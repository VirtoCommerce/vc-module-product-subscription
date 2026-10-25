using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.Search;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Renewals;

/// <summary>
/// The renewal state machine. Every step is safe to run twice: order placement is guarded by the stored order id,
/// charges by an idempotency key per attempt, and every change runs under the customer's lock
/// (<see cref="SubscriptionLocks"/>), which customer and admin actions on subscriptions take too.
/// </summary>
public class RenewalProcessor(
    ISubscriptionService subscriptionService,
    ISubscriptionSearchService subscriptionSearchService,
    IRenewalService renewalService,
    IRenewalSearchService renewalSearchService,
    IRenewalCartBuilder cartBuilder,
    ICustomerOrderService customerOrderService,
    IPaymentFlowService paymentFlowService,
    IPaymentMethodsSearchService paymentMethodsSearchService,
    IStoredPaymentMethodService storedPaymentMethodService,
    IStoreService storeService,
    IStoreSearchService storeSearchService,
    ISettingsManager settingsManager,
    ISubscriptionNotificationService notificationService,
    IDistributedLock distributedLock,
    ILogger<RenewalProcessor> logger)
    : IRenewalProcessor
{
    private const int MaxSteps = 12;
    private const int MaxProviderErrors = 5;
    private const int SchedulerPageSize = 500;
    private const int MaxSchedulerPages = 1000;
    private static readonly TimeSpan _providerErrorRetryDelay = TimeSpan.FromMinutes(15);
    // How long to wait for the result of a retry the payment provider runs on its own, before giving up on it.
    private static readonly TimeSpan _providerRetryWait = TimeSpan.FromDays(14);
    private static readonly TimeSpan _orderNowRepeatWindow = TimeSpan.FromMinutes(2);
    // The scheduler doesn't wait long for a busy customer: the next run picks them up.
    private static readonly TimeSpan _schedulerLockWait = TimeSpan.FromSeconds(5);
    private static readonly PaymentStatus[] _paidStatuses = [PaymentStatus.Authorized, PaymentStatus.Paid];
    private static readonly PaymentStatus[] _failedStatuses = [PaymentStatus.Declined, PaymentStatus.Voided, PaymentStatus.Cancelled];

    protected virtual DateTime UtcNow => DateTime.UtcNow;

    #region Scheduler

    public virtual async Task<int> ProcessDueAsync()
    {
        var now = UtcNow;

        await ResumePausedSubscriptionsAsync(now);
        await CreateDueRenewalsAsync(now);

        var processed = 0;
        var criteria = AbstractTypeFactory<RenewalSearchCriteria>.TryCreateInstance();
        // Read the clock again: renewals created just above are due "now" too and get their reminder in this run.
        criteria.DueBefore = UtcNow;
        criteria.OnlyOpen = true;
        criteria.Take = 50;
        criteria.Sort = "NextProcessingDate";

        var seen = new HashSet<string>();
        while (true)
        {
            var due = (await renewalSearchService.SearchAsync(criteria, clone: false)).Results.Where(x => seen.Add(x.Id)).ToList();
            if (due.Count == 0)
            {
                break;
            }

            foreach (var renewal in due)
            {
                try
                {
                    await ProcessAsync(renewal.Id);
                    processed++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process renewal {RenewalNumber}", renewal.Number);
                }
            }
        }

        await SendAnnualRemindersAsync(now);

        return processed;
    }

    protected virtual async Task ResumePausedSubscriptionsAsync(DateTime now)
    {
        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.Statuses = [SubscriptionStatus.Paused];
        criteria.PausedUntilBefore = now;
        criteria.Sort = "PausedUntil;Id";
        criteria.Take = SchedulerPageSize;

        var leadDaysByStore = (await GetStoresAsync()).ToDictionary(x => x.Id, x => x.Settings.GetValue<int>(ModuleConstants.Settings.Store.ReminderLeadDays));

        await ForEachSubscriptionPageAsync(criteria, async page =>
        {
            var unchanged = 0;
            foreach (var customerGroup in page.GroupBy(x => x.CustomerId))
            {
                var ids = customerGroup.Select(x => x.Id).ToList();
                var ran = await TryRunForCustomerAsync(customerGroup.Key, ids, async () =>
                {
                    // Read them again under the lock: the customer may have resumed or cancelled them since the search.
                    var toResume = (await subscriptionService.GetAsync(ids))
                        .Where(x => x.Status == SubscriptionStatus.Paused && x.PausedUntil != null && x.PausedUntil <= now)
                        .ToList();

                    foreach (var subscription in toResume)
                    {
                        subscription.Status = SubscriptionStatus.Active;
                        subscription.PausedUntil = null;

                        // Leave the reminder its lead days before the next order.
                        var earliest = SubscriptionSchedule.EarliestNextOrderDate(now, leadDaysByStore.GetValueOrDefault(subscription.StoreId));
                        if (subscription.NextOrderDate < earliest)
                        {
                            subscription.NextOrderDate = earliest;
                        }
                    }

                    if (toResume.Count > 0)
                    {
                        await subscriptionService.SaveChangesAsync(toResume);
                    }
                });

                if (!ran)
                {
                    unchanged += ids.Count;
                }
            }

            return unchanged;
        });
    }

    protected virtual async Task CreateDueRenewalsAsync(DateTime now)
    {
        foreach (var store in await GetStoresAsync())
        {
            var leadDays = Math.Max(0, store.Settings.GetValue<int>(ModuleConstants.Settings.Store.ReminderLeadDays));

            var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
            criteria.StoreId = store.Id;
            criteria.Statuses = [SubscriptionStatus.Active];
            criteria.WithoutOpenRenewal = true;
            criteria.NextOrderDateBefore = now.AddDays(leadDays);
            criteria.Sort = "NextOrderDate;Id";
            criteria.Take = SchedulerPageSize;

            await ForEachSubscriptionPageAsync(criteria, async page =>
            {
                var unchanged = 0;
                foreach (var group in page.GroupBy(GetGroupKey))
                {
                    unchanged += await TryCreateDueRenewalAsync(group.ToList(), store, criteria.NextOrderDateBefore.Value);
                }

                return unchanged;
            });
        }
    }

    /// <summary>
    /// Creates the renewal for a group of due subscriptions of one customer (one key of <see cref="GetGroupKey"/>), under
    /// that customer's lock. Returns how many of them were left due and without a renewal (e.g. the lock was busy), so
    /// paging skips them.
    /// </summary>
    protected virtual async Task<int> TryCreateDueRenewalAsync(IList<Subscription> group, Store store, DateTime dueBefore)
    {
        var ids = group.Select(x => x.Id).ToList();
        var groupKey = GetGroupKey(group[0]);
        var ran = await TryRunForCustomerAsync(group[0].CustomerId, ids, async () =>
        {
            // Search again under the lock: an "order now", a change or another scheduler run may have got there first.
            // Search by customer, not by these ids: part of the group may be on the next page of the scheduler's search.
            var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
            criteria.StoreId = store.Id;
            criteria.CustomerId = group[0].CustomerId;
            criteria.Statuses = [SubscriptionStatus.Active];
            criteria.WithoutOpenRenewal = true;
            criteria.NextOrderDateBefore = dueBefore;
            criteria.Take = SchedulerPageSize;

            var due = (await subscriptionSearchService.SearchAsync(criteria)).Results
                .Where(x => GetGroupKey(x) == groupKey)
                .ToList();

            foreach (var part in SplitByProductDiscount(due))
            {
                await CreateRenewalAsync(part, store, isManual: false);
            }
        });

        return ran ? 0 : ids.Count;
    }

    /// <summary>
    /// A renewal order has one line per product, priced with one discount: subscriptions to the same product with
    /// different discounts go to separate renewals.
    /// </summary>
    protected virtual IList<IList<Subscription>> SplitByProductDiscount(IList<Subscription> subscriptions)
    {
        var parts = new List<IList<Subscription>>();
        foreach (var subscription in subscriptions)
        {
            var part = parts.FirstOrDefault(x => x.All(other => other.ProductId != subscription.ProductId || HasSameDiscount(other, subscription)));
            if (part == null)
            {
                part = new List<Subscription>();
                parts.Add(part);
            }

            part.Add(subscription);
        }

        return parts;
    }

    protected static bool HasSameDiscount(Subscription first, Subscription second)
    {
        return first.PlanId == second.PlanId && first.DiscountType == second.DiscountType && first.DiscountValue == second.DiscountValue;
    }

    /// <summary>
    /// Pages through the subscriptions matching <paramref name="criteria"/> while <paramref name="processPage"/> changes
    /// them. A processed subscription stops matching, so the next page starts after the ones left as they were:
    /// <paramref name="processPage"/> returns how many of its page those are.
    /// </summary>
    protected virtual async Task ForEachSubscriptionPageAsync(SubscriptionSearchCriteria criteria, Func<IList<Subscription>, Task<int>> processPage)
    {
        var unchanged = 0;
        for (var pageNumber = 0; pageNumber < MaxSchedulerPages; pageNumber++)
        {
            criteria.Skip = unchanged;
            var page = (await subscriptionSearchService.SearchAsync(criteria)).Results;
            if (page.Count == 0)
            {
                return;
            }

            unchanged += await processPage(page);

            if (page.Count < criteria.Take)
            {
                return;
            }
        }
    }

    protected virtual async Task<IList<Store>> GetStoresAsync()
    {
        var criteria = AbstractTypeFactory<StoreSearchCriteria>.TryCreateInstance();
        return await storeSearchService.SearchAllNoCloneAsync(criteria);
    }

    /// <summary>A scheduler step for one customer: skipped (false) when their lock stays busy or the step fails.</summary>
    protected virtual async Task<bool> TryRunForCustomerAsync(string customerId, IList<string> subscriptionIds, Func<Task> action)
    {
        try
        {
            return await distributedLock.TryRunForCustomerAsync(customerId, action, _schedulerLockWait);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduler step failed for subscriptions {Ids}", string.Join(", ", subscriptionIds));
            return false;
        }
    }

    /// <summary>Subscriptions with the same key become one renewal and one order.</summary>
    protected virtual string GetGroupKey(Subscription subscription)
    {
        var address = subscription.ShippingAddress;
        var addressKey = address == null
            ? string.Empty
            : string.Join("|", address.Line1, address.Line2, address.City, address.PostalCode, address.RegionId, address.CountryCode, address.FirstName, address.LastName);

        return string.Join("#",
            subscription.StoreId,
            subscription.CustomerId,
            subscription.NextOrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            subscription.StoredPaymentMethodId,
            subscription.PaymentMethodCode,
            subscription.ShippingMethodCode,
            subscription.ShippingMethodOption,
            subscription.Currency,
            addressKey);
    }

    protected virtual async Task<Renewal> CreateRenewalAsync(IList<Subscription> subscriptions, Store store, bool isManual)
    {
        var now = UtcNow;
        var first = subscriptions[0];
        var cutoffHours = store.Settings.GetValue<int>(ModuleConstants.Settings.Store.CutoffHours);
        var scheduledDate = isManual ? now : subscriptions.Min(x => x.NextOrderDate);
        var cutoff = isManual ? now : scheduledDate.AddHours(-cutoffHours);
        if (cutoff < now)
        {
            cutoff = now;
        }

        var renewal = AbstractTypeFactory<Renewal>.TryCreateInstance();
        renewal.StoreId = first.StoreId;
        renewal.CustomerId = first.CustomerId;
        renewal.CustomerName = first.CustomerName;
        renewal.CustomerEmail = first.CustomerEmail;
        renewal.OrganizationId = first.OrganizationId;
        renewal.Currency = first.Currency;
        renewal.LanguageCode = first.LanguageCode;
        renewal.ScheduledDate = scheduledDate;
        renewal.CutoffDate = cutoff;
        renewal.NextProcessingDate = now;
        renewal.State = RenewalState.Scheduled;
        renewal.IsManual = isManual;
        renewal.PaymentMethodCode = first.PaymentMethodCode;
        renewal.StoredPaymentMethodId = first.StoredPaymentMethodId;
        renewal.ShippingAddress = first.ShippingAddress;
        renewal.ShippingMethodCode = first.ShippingMethodCode;
        renewal.ShippingMethodOption = first.ShippingMethodOption;
        renewal.Items = subscriptions.Select(x =>
        {
            var item = AbstractTypeFactory<RenewalItem>.TryCreateInstance();
            item.SubscriptionId = x.Id;
            item.ProductId = x.ProductId;
            item.Sku = x.Sku;
            item.ProductName = x.ProductName;
            item.ImageUrl = x.ImageUrl;
            item.Quantity = x.Quantity;
            return item;
        }).ToList();
        renewal.AddEvent(RenewalEventType.StateChanged, isManual ? "Order now requested" : $"Renewal scheduled for {scheduledDate:yyyy-MM-dd}");

        await renewalService.SaveChangesAsync([renewal]);

        foreach (var subscription in subscriptions)
        {
            subscription.CurrentRenewalId = renewal.Id;
        }

        await subscriptionService.SaveChangesAsync(subscriptions);

        return renewal;
    }

    protected virtual async Task SendAnnualRemindersAsync(DateTime now)
    {
        var stores = (await GetStoresAsync()).Where(x => x.Settings.GetValue<bool>(ModuleConstants.Settings.Store.AnnualReminderEnabled));
        foreach (var store in stores)
        {
            var dueBefore = now.AddYears(-1);
            var activeStatuses = new[] { SubscriptionStatus.Active, SubscriptionStatus.Paused };

            var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
            criteria.StoreId = store.Id;
            criteria.Statuses = activeStatuses;
            criteria.AnnualReminderDueBefore = dueBefore;
            criteria.Sort = "StartDate;Id";
            criteria.Take = SchedulerPageSize;

            await ForEachSubscriptionPageAsync(criteria, async page =>
            {
                var unchanged = 0;
                foreach (var customerGroup in page.GroupBy(x => x.CustomerId))
                {
                    var ids = customerGroup.Select(x => x.Id).ToList();
                    var ran = await TryRunForCustomerAsync(customerGroup.Key, ids, async () =>
                    {
                        var group = (await subscriptionService.GetAsync(ids))
                            .Where(x => activeStatuses.Contains(x.Status) && (x.LastAnnualReminderDate ?? x.StartDate) <= dueBefore)
                            .ToList();
                        if (group.Count == 0)
                        {
                            return;
                        }

                        await notificationService.SendAsync<SubscriptionAnnualReminderEmailNotification>(group);
                        foreach (var subscription in group)
                        {
                            subscription.LastAnnualReminderDate = now;
                        }

                        await subscriptionService.SaveChangesAsync(group);
                    });

                    if (!ran)
                    {
                        unchanged += ids.Count;
                    }
                }

                return unchanged;
            });
        }
    }

    #endregion

    #region Commands

    public virtual async Task<Renewal> ProcessAsync(string renewalId, bool ignoreSchedule = false)
    {
        var customerId = (await renewalService.GetNoCloneAsync(renewalId))?.CustomerId;
        if (customerId == null)
        {
            return null;
        }

        Renewal result = null;
        var ran = await distributedLock.TryRunForCustomerAsync(customerId,
            async () => { result = await ProcessLockedAsync(renewalId, ignoreSchedule); });

        return ran ? result : await renewalService.GetByIdAsync(renewalId);
    }

    public virtual async Task<Renewal> OrderNowAsync(string subscriptionId)
    {
        var customerId = (await subscriptionService.GetNoCloneAsync(subscriptionId))?.CustomerId
            ?? throw new SubscriptionOperationException("Subscription not found.");

        // Under the customer's lock: a double click, a retried request or a scheduler run never makes a second renewal.
        return await distributedLock.RunForCustomerAsync(customerId, () => OrderNowLockedAsync(subscriptionId));
    }

    protected virtual async Task<Renewal> OrderNowLockedAsync(string subscriptionId)
    {
        var subscription = await subscriptionService.GetByIdAsync(subscriptionId)
            ?? throw new SubscriptionOperationException("Subscription not found.");

        if (subscription.Status != SubscriptionStatus.Active)
        {
            throw new SubscriptionOperationException("Only active subscriptions can be ordered now.");
        }

        // The lock makes a double click or a retried request wait for the first one; this stops it ordering again.
        if (subscription.LastOrderDate > UtcNow.Subtract(_orderNowRepeatWindow))
        {
            throw new SubscriptionOperationException("This subscription was just ordered. Check your orders before ordering again.");
        }

        if (!string.IsNullOrEmpty(subscription.CurrentRenewalId))
        {
            var open = await renewalService.GetByIdAsync(subscription.CurrentRenewalId);
            if (open != null && open.IsOpen && open.Items.Any(x => x.SubscriptionId == subscription.Id))
            {
                if (!RenewalState.BeforeOrder.Contains(open.State))
                {
                    throw new SubscriptionOperationException("An order for this subscription is already in progress.");
                }

                open.IsManual = true;
                open.CutoffDate = UtcNow;
                open.NextProcessingDate = UtcNow;
                open.AddEvent(RenewalEventType.CustomerAction, "Order now requested");
                await renewalService.SaveChangesAsync([open]);
                return await ProcessLockedAsync(open.Id, ignoreSchedule: true);
            }
        }

        // A placed order still waiting for its payment (retry or customer confirmation) must finish first,
        // or the customer could end up with two orders for the same delivery.
        var placedCriteria = AbstractTypeFactory<RenewalSearchCriteria>.TryCreateInstance();
        placedCriteria.SubscriptionId = subscription.Id;
        placedCriteria.States = [RenewalState.Ordered, RenewalState.PaymentActionRequired, RenewalState.RetryScheduled];
        placedCriteria.Take = 1;
        if ((await renewalSearchService.SearchAsync(placedCriteria)).TotalCount > 0)
        {
            throw new SubscriptionOperationException("Your last order for this subscription is still waiting for payment. Complete it first.");
        }

        var store = await storeService.GetNoCloneAsync(subscription.StoreId)
            ?? throw new SubscriptionOperationException("The subscription's store was not found.");
        var renewal = await CreateRenewalAsync([subscription], store, isManual: true);
        return await ProcessLockedAsync(renewal.Id, ignoreSchedule: true);
    }

    public virtual Task<Renewal> AcceptReviewAsync(string renewalId)
    {
        return WithLockAsync(renewalId, async renewal =>
        {
            if (renewal.State != RenewalState.ReviewRequired)
            {
                throw new SubscriptionOperationException("This renewal doesn't need a review.");
            }

            foreach (var item in renewal.Items.Where(x => !x.IsAvailable))
            {
                item.IsExcluded = true;
            }

            renewal.NotifiedTotal = null;
            renewal.ReviewReason = null;
            renewal.CutoffDate = UtcNow;
            renewal.AddEvent(RenewalEventType.CustomerAction, "Customer accepted the changes");
            renewal.ChangeState(RenewalState.Notified, "Review accepted", UtcNow);
            await renewalService.SaveChangesAsync([renewal]);

            return await ProcessLockedAsync(renewal.Id, ignoreSchedule: true);
        });
    }

    public virtual Task<Renewal> SkipAsync(string renewalId)
    {
        return WithLockAsync(renewalId, async renewal =>
        {
            if (!RenewalState.BeforeOrder.Contains(renewal.State))
            {
                throw new SubscriptionOperationException("This renewal can no longer be skipped: the order has been placed.");
            }

            var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
            foreach (var subscription in subscriptions)
            {
                subscription.NextOrderDate = SubscriptionSchedule.NextAfter(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount, UtcNow, subscription.EffectiveAnchorDay);
                subscription.SkipCount++;
                if (subscription.CurrentRenewalId == renewal.Id)
                {
                    subscription.CurrentRenewalId = null;
                }
            }

            await subscriptionService.SaveChangesAsync(subscriptions);

            renewal.AddEvent(RenewalEventType.CustomerAction, "Delivery skipped");
            renewal.ChangeState(RenewalState.Skipped, "Delivery skipped");
            await renewalService.SaveChangesAsync([renewal]);
            return renewal;
        });
    }

    public virtual Task<Renewal> CancelAsync(string renewalId, string reason)
    {
        return WithLockAsync(renewalId, async renewal =>
        {
            if (!RenewalState.BeforeOrder.Contains(renewal.State))
            {
                throw new SubscriptionOperationException("This renewal can no longer be cancelled: the order has been placed.");
            }

            var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
            foreach (var subscription in subscriptions)
            {
                subscription.NextOrderDate = SubscriptionSchedule.NextAfter(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount, UtcNow, subscription.EffectiveAnchorDay);
                if (subscription.CurrentRenewalId == renewal.Id)
                {
                    subscription.CurrentRenewalId = null;
                }
            }

            await subscriptionService.SaveChangesAsync(subscriptions);

            renewal.ChangeState(RenewalState.Cancelled, string.IsNullOrWhiteSpace(reason) ? "Cancelled by an administrator" : reason);
            await renewalService.SaveChangesAsync([renewal]);
            return renewal;
        });
    }

    public virtual Task<Renewal> RetryPaymentAsync(string renewalId)
    {
        return WithLockAsync(renewalId, async renewal =>
        {
            if (renewal.State is not (RenewalState.Failed or RenewalState.RetryScheduled or RenewalState.PaymentActionRequired))
            {
                throw new SubscriptionOperationException("There is no failed payment to retry for this renewal.");
            }

            if (IsProviderRetry(renewal))
            {
                // A second charge now could collect the payment twice.
                throw new SubscriptionOperationException("The payment provider is retrying this payment. We'll let you know the result.");
            }

            if (string.IsNullOrEmpty(renewal.OrderId))
            {
                throw new SubscriptionOperationException("The renewal has no order to pay.");
            }

            // Use the card currently set on the subscription: the customer may have just replaced it.
            var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
            var withCard = subscriptions.FirstOrDefault(x => !string.IsNullOrEmpty(x.StoredPaymentMethodId));
            if (withCard != null)
            {
                renewal.StoredPaymentMethodId = withCard.StoredPaymentMethodId;
            }

            renewal.AddEvent(RenewalEventType.CustomerAction, "Payment retry requested");
            renewal.ChangeState(RenewalState.Ordered, "Retrying the payment", UtcNow);
            await renewalService.SaveChangesAsync([renewal]);

            return await ProcessLockedAsync(renewal.Id, ignoreSchedule: true);
        });
    }

    public virtual async Task HandleOrderChangedAsync(CustomerOrder order)
    {
        if (order == null || order.IsPrototype || string.IsNullOrEmpty(order.Id))
        {
            return;
        }

        var criteria = AbstractTypeFactory<RenewalSearchCriteria>.TryCreateInstance();
        criteria.OrderId = order.Id;
        criteria.Take = 1;
        var found = (await renewalSearchService.SearchAsync(criteria, clone: false)).Results.FirstOrDefault();
        if (found == null)
        {
            return;
        }

        // Wait for the customer's lock instead of skipping: this runs in a background job, and a change dropped here
        // (e.g. the shipment that triggers the capture) never comes again. If the lock stays busy, the job fails and
        // the background job engine retries it.
        await distributedLock.RunForCustomerAsync(found.CustomerId, () => HandleOrderChangedLockedAsync(found.Id, order.Id));
    }

    protected virtual async Task HandleOrderChangedLockedAsync(string renewalId, string orderId)
    {
        var renewal = await renewalService.GetByIdAsync(renewalId);
        if (renewal == null)
        {
            return;
        }

        var freshOrder = await customerOrderService.GetByIdAsync(orderId);
        var payment = freshOrder?.InPayments?.FirstOrDefault(x => x.Id == renewal.PaymentId) ?? freshOrder?.InPayments?.FirstOrDefault();
        if (payment == null)
        {
            return;
        }

        var changed = false;

        if (renewal.State is RenewalState.PaymentActionRequired or RenewalState.RetryScheduled or RenewalState.Failed or RenewalState.Ordered
            && _paidStatuses.Contains(payment.PaymentStatus))
        {
            var collectedByProvider = IsProviderRetry(renewal);
            renewal.AuthorizedDate = UtcNow;
            renewal.AuthorizationExpiresDate = UtcNow.AddMinutes(await GetAuthorizationLifetimeMinutesAsync());
            renewal.LastErrorMessage = null;
            renewal.NextAttemptDate = null;
            renewal.ChangeState(payment.PaymentStatus == PaymentStatus.Paid ? RenewalState.Completed : RenewalState.Authorized,
                collectedByProvider ? "The payment provider collected the payment" : "Payment completed by the customer");
            await ReactivateSubscriptionsAsync(renewal);
            changed = true;
        }
        else if (IsProviderRetry(renewal) && _failedStatuses.Contains(payment.PaymentStatus))
        {
            await FailAsync(renewal, "We couldn't charge your card. Please update your payment method.", markCardForUpdate: false);
            changed = true;
        }

        if (renewal.State == RenewalState.Authorized)
        {
            if (payment.PaymentStatus == PaymentStatus.Paid)
            {
                renewal.CapturedDate ??= UtcNow;
                renewal.ChangeState(RenewalState.Completed, "Payment captured");
                changed = true;
            }
            else if (IsShipped(freshOrder))
            {
                await CaptureAsync(renewal, freshOrder, payment);
                changed = true;
            }
        }

        if (changed)
        {
            await renewalService.SaveChangesAsync([renewal]);
            if (!renewal.IsOpen)
            {
                await ReleaseSubscriptionsAsync(renewal);
            }
        }
    }

    #endregion

    #region State machine

    protected virtual async Task<Renewal> ProcessLockedAsync(string renewalId, bool ignoreSchedule)
    {
        var renewal = await renewalService.GetByIdAsync(renewalId);
        if (renewal == null || !renewal.IsOpen)
        {
            return renewal;
        }

        var store = await storeService.GetNoCloneAsync(renewal.StoreId);
        if (store == null)
        {
            // Nothing can be priced, ordered or charged without the store: end the renewal instead of failing on every run.
            renewal.AddEvent(RenewalEventType.Error, $"The store {renewal.StoreId} no longer exists.");
            renewal.ChangeState(RenewalState.Cancelled, "The store no longer exists");
            await renewalService.SaveChangesAsync([renewal]);
            await ReleaseSubscriptionsAsync(renewal);
            return renewal;
        }

        for (var step = 0; step < MaxSteps; step++)
        {
            var before = renewal.State;
            var now = UtcNow;

            try
            {
                switch (renewal.State)
                {
                    case RenewalState.Scheduled:
                        await PrepareAndNotifyAsync(renewal);
                        break;

                    case RenewalState.Notified when ignoreSchedule || now >= renewal.CutoffDate:
                        await LockAndPlaceOrderAsync(renewal, store);
                        break;

                    case RenewalState.ReviewRequired when !ignoreSchedule && renewal.NextProcessingDate <= now:
                        await SkipUnreviewedAsync(renewal);
                        break;

                    case RenewalState.Ordered:
                        await ChargeAsync(renewal, store);
                        break;

                    case RenewalState.RetryScheduled when IsProviderRetry(renewal):
                        // The provider retries on its own; its result arrives as an order change. Never charge here.
                        if (renewal.NextProcessingDate <= now)
                        {
                            await EndProviderRetryAsync(renewal);
                        }

                        break;

                    case RenewalState.RetryScheduled when ignoreSchedule || renewal.NextAttemptDate <= now:
                        renewal.ChangeState(RenewalState.Ordered, "Retrying the payment", now);
                        await ChargeAsync(renewal, store);
                        break;

                    case RenewalState.PaymentActionRequired when !ignoreSchedule && renewal.NextProcessingDate <= now:
                        await FailAsync(renewal, "The payment wasn't confirmed in time.", markCardForUpdate: false);
                        break;

                    case RenewalState.Authorized:
                        var order = await customerOrderService.GetByIdAsync(renewal.OrderId);
                        var payment = order?.InPayments?.FirstOrDefault(x => x.Id == renewal.PaymentId) ?? order?.InPayments?.FirstOrDefault();
                        if (payment != null && IsShipped(order) && payment.PaymentStatus == PaymentStatus.Authorized)
                        {
                            await CaptureAsync(renewal, order, payment);
                        }
                        else
                        {
                            renewal.NextProcessingDate = null;
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Renewal {RenewalNumber} failed in state {State}", renewal.Number, renewal.State);
                renewal.AddEvent(RenewalEventType.Error, "Processing error; will try again later.", ex.Message);
                renewal.NextProcessingDate = now.AddMinutes(30);
                await renewalService.SaveChangesAsync([renewal]);
                break;
            }

            await renewalService.SaveChangesAsync([renewal]);

            if (renewal.State == before || !renewal.IsOpen)
            {
                break;
            }
        }

        if (!renewal.IsOpen)
        {
            await ReleaseSubscriptionsAsync(renewal);
        }

        return renewal;
    }

    protected virtual async Task PrepareAndNotifyAsync(Renewal renewal)
    {
        var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: true);
        RemoveInactiveItems(renewal, subscriptions);

        if (renewal.Items.Count == 0)
        {
            renewal.ChangeState(RenewalState.Cancelled, "No active subscriptions left in this renewal");
            return;
        }

        var cart = await cartBuilder.BuildAsync(renewal, subscriptions);
        ApplyCartResult(renewal, cart);
        renewal.PreviewTotal = cart.Total;
        renewal.NotifiedTotal = cart.Total;
        renewal.AddEvent(RenewalEventType.Calculated, $"Preview total {cart.Total:0.00} {renewal.Currency}", cart.Problems.Count > 0 ? string.Join("; ", cart.Problems) : null);

        if (!renewal.IsManual)
        {
            await notificationService.SendAsync<RenewalReminderEmailNotification>(subscriptions, renewal);
            renewal.ReminderSentDate = UtcNow;
            renewal.AddEvent(RenewalEventType.ReminderSent, $"Reminder sent; the order will be placed after {renewal.CutoffDate:yyyy-MM-dd HH:mm} UTC");
        }

        renewal.ChangeState(RenewalState.Notified, renewal.IsManual ? "Placing the order now" : "Waiting for the cutoff", renewal.CutoffDate);
    }

    protected virtual async Task LockAndPlaceOrderAsync(Renewal renewal, Store store)
    {
        if (!string.IsNullOrEmpty(renewal.OrderId))
        {
            renewal.ChangeState(RenewalState.Ordered, "Order already placed", UtcNow);
            return;
        }

        var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: true);
        RemoveInactiveItems(renewal, subscriptions);

        if (renewal.Items.Count(x => !x.IsExcluded) == 0)
        {
            await AdvanceSubscriptionsAsync(renewal, subscriptions, order: null);
            renewal.ChangeState(RenewalState.Skipped, "No items left to order in this delivery");
            return;
        }

        var cart = await cartBuilder.BuildAsync(renewal, subscriptions);
        ApplyCartResult(renewal, cart);

        var reviewReason = GetReviewReason(renewal, cart);
        if (reviewReason != null)
        {
            renewal.ReviewReason = reviewReason;
            var waitDays = store.Settings.GetValue<int>(ModuleConstants.Settings.Store.PaymentActionWaitDays);
            renewal.ChangeState(RenewalState.ReviewRequired, reviewReason, UtcNow.AddDays(Math.Max(1, waitDays)));
            await notificationService.SendAsync<RenewalReviewRequiredEmailNotification>(subscriptions, renewal);
            return;
        }

        var order = await cartBuilder.PlaceOrderAsync(renewal, cart);

        renewal.OrderId = order.Id;
        renewal.OrderNumber = order.Number;
        renewal.PaymentId = order.InPayments?.FirstOrDefault()?.Id;
        renewal.OrderTotal = order.Total;
        renewal.AddEvent(RenewalEventType.StateChanged, $"Order {order.Number} placed for {order.Total:0.00} {order.Currency}");

        // Persist the order link right away, so a crash never places a second order.
        await renewalService.SaveChangesAsync([renewal]);

        await AdvanceSubscriptionsAsync(renewal, subscriptions, order);

        renewal.ChangeState(RenewalState.Ordered, $"Order {order.Number} placed", UtcNow);
    }

    protected virtual string GetReviewReason(Renewal renewal, RenewalCartResult cart)
    {
        if (cart.HasUnavailableItems)
        {
            var names = renewal.Items.Where(x => !x.IsAvailable && !x.IsExcluded).Select(x => x.ProductName);
            return $"Some items are not available: {string.Join(", ", names)}.";
        }

        if (cart.Problems.Count > 0 && cart.Aggregate.Cart.Shipments.Count == 0)
        {
            return "No shipping method is available for the delivery address.";
        }

        if (renewal.NotifiedTotal != null && cart.Total > renewal.NotifiedTotal.Value + 0.005m)
        {
            return $"The total went up from {renewal.NotifiedTotal.Value:0.00} to {cart.Total:0.00} {renewal.Currency}. Please confirm the new price.";
        }

        return null;
    }

    protected virtual async Task SkipUnreviewedAsync(Renewal renewal)
    {
        var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
        await AdvanceSubscriptionsAsync(renewal, subscriptions, order: null);
        renewal.ChangeState(RenewalState.Skipped, "Skipped: the changes were not reviewed in time");
    }

    protected virtual async Task ChargeAsync(Renewal renewal, Store store)
    {
        var order = await customerOrderService.GetByIdAsync(renewal.OrderId);
        var payment = order?.InPayments?.FirstOrDefault(x => x.Id == renewal.PaymentId) ?? order?.InPayments?.FirstOrDefault();

        if (order == null || payment == null)
        {
            renewal.ChangeState(RenewalState.Completed, "Order placed without a payment; payment is collected separately");
            return;
        }

        if (_paidStatuses.Contains(payment.PaymentStatus))
        {
            renewal.AuthorizedDate ??= UtcNow;
            renewal.ChangeState(payment.PaymentStatus == PaymentStatus.Paid ? RenewalState.Completed : RenewalState.Authorized, "Payment already authorized");
            return;
        }

        var paymentMethod = await GetPaymentMethodAsync(order.StoreId, payment.GatewayCode);
        var storedMethod = string.IsNullOrEmpty(renewal.StoredPaymentMethodId) ? null : await storedPaymentMethodService.GetByIdAsync(renewal.StoredPaymentMethodId);

        if (paymentMethod is not ISupportStoredCredentialFlow)
        {
            // E.g. invoice terms: the order is the deliverable and payment follows outside the subscription.
            renewal.ChangeState(RenewalState.Completed, "Order placed. This payment method doesn't support automatic charging; payment is collected separately.");
            return;
        }

        if (storedMethod == null)
        {
            // A card payment with no card on file (e.g. an imported subscription): the customer pays on the order page,
            // where the card can be saved for the next deliveries.
            await OnRequiresActionAsync(renewal, store, order,
                "Please add a card to pay for this delivery.",
                "No saved card on the subscription; the customer has to pay");
            return;
        }

        if (storedMethod.Status != StoredPaymentMethodStatus.Active)
        {
            await FailAsync(renewal, "Your saved card needs to be updated before we can charge this delivery.", markCardForUpdate: false);
            return;
        }

        renewal.AttemptCount++;
        var request = new ProcessStoredPaymentRequest
        {
            OrderId = order.Id,
            Order = order,
            PaymentId = payment.Id,
            Payment = payment,
            StoreId = order.StoreId,
            Store = store,
            CultureName = order.LanguageCode,
            StoredPaymentMethod = storedMethod,
            Amount = payment.Sum,
            Currency = order.Currency,
            IdempotencyKey = $"{renewal.IdempotencyKey}-{renewal.AttemptCount}",
            Usage = StoredCredentialUsage.Recurring,
            Sequence = StoredCredentialSequence.Subsequent,
            Reason = renewal.AttemptCount > 1 ? MerchantInitiatedReason.Resubmission : MerchantInitiatedReason.Recurring,
            CaptureMode = StoredPaymentCaptureMode.AuthorizeOnly,
        };

        ProcessStoredPaymentRequestResult result;
        try
        {
            result = await paymentMethod.ProcessStoredPaymentAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stored payment failed for renewal {RenewalNumber}", renewal.Number);
            result = new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.ProviderError, RawMessage = ex.Message };
        }

        renewal.LastOutcome = result.Outcome.ToString();
        renewal.AddEvent(RenewalEventType.PaymentAttempt,
            $"Payment attempt {renewal.AttemptCount}: {result.Outcome}",
            JsonConvert.SerializeObject(new { result.RawCode, result.AdviceCode, result.RawMessage, result.TransactionId, request.IdempotencyKey }));

        switch (result.Outcome)
        {
            case ChargeOutcome.Succeeded:
                await OnChargeSucceededAsync(renewal, order, payment, storedMethod, result);
                break;

            case ChargeOutcome.RequiresAction:
                await OnRequiresActionAsync(renewal, store, order, recoveryUrl: result.RecoveryUrl);
                break;

            case ChargeOutcome.SoftDecline:
                await OnSoftDeclineAsync(renewal, store, result);
                break;

            case ChargeOutcome.ProviderError:
                await OnProviderErrorAsync(renewal);
                break;

            default:
                storedMethod.Status = StoredPaymentMethodStatus.NeedsUpdate;
                await storedPaymentMethodService.SaveChangesAsync([storedMethod]);
                payment.PaymentStatus = PaymentStatus.Declined;
                payment.Status = PaymentStatus.Declined.ToString();
                await customerOrderService.SaveChangesAsync([order]);
                await FailAsync(renewal, "Your card was declined. Please update your payment method.", markCardForUpdate: true);
                break;
        }
    }

    protected virtual async Task OnChargeSucceededAsync(Renewal renewal, CustomerOrder order, PaymentIn payment, StoredPaymentMethod storedMethod, ProcessStoredPaymentRequestResult result)
    {
        var now = UtcNow;
        var paid = result.NewPaymentStatus == PaymentStatus.Paid;

        payment.PaymentStatus = paid ? PaymentStatus.Paid : PaymentStatus.Authorized;
        payment.Status = payment.PaymentStatus.ToString();
        payment.OuterId = result.TransactionId;
        payment.AuthorizedDate = now;
        payment.IsApproved = true;
        if (paid)
        {
            payment.CapturedDate = now;
        }

        await customerOrderService.SaveChangesAsync([order]);

        storedMethod.LastUsedDate = now;
        if (string.IsNullOrEmpty(storedMethod.NetworkTransactionId) && !string.IsNullOrEmpty(result.NetworkTransactionId))
        {
            storedMethod.NetworkTransactionId = result.NetworkTransactionId;
        }

        await storedPaymentMethodService.SaveChangesAsync([storedMethod]);

        renewal.AuthorizedDate = now;
        renewal.AuthorizationExpiresDate = now.AddMinutes(await GetAuthorizationLifetimeMinutesAsync());
        renewal.LastErrorMessage = null;
        renewal.NextAttemptDate = null;
        await ReactivateSubscriptionsAsync(renewal);

        if (paid)
        {
            renewal.CapturedDate = now;
            renewal.ChangeState(RenewalState.Completed, "Payment captured");
        }
        else
        {
            renewal.ChangeState(RenewalState.Authorized, "Payment authorized; it is captured when the order ships");
        }
    }

    protected virtual async Task OnRequiresActionAsync(
        Renewal renewal,
        Store store,
        CustomerOrder order,
        string customerMessage = "Your bank asked you to confirm this payment.",
        string reason = "The bank requires authentication (3-D Secure)",
        string recoveryUrl = null)
    {
        var waitDays = store.Settings.GetValue<int>(ModuleConstants.Settings.Store.PaymentActionWaitDays);
        renewal.LastErrorMessage = customerMessage;
        renewal.ChangeState(RenewalState.PaymentActionRequired, reason, UtcNow.AddDays(Math.Max(1, waitDays)));

        var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
        // The provider's own page when it gives one (StoredCredentialResultBase.RecoveryUrl), otherwise the storefront's order payment page.
        var paymentUrl = recoveryUrl;
        if (string.IsNullOrEmpty(paymentUrl))
        {
            var storeUrl = await notificationService.GetStoreUrlAsync(renewal.StoreId);
            paymentUrl = $"{storeUrl}account/orders/{order.Id}/payment";
        }
        await notificationService.SendAsync<RenewalPaymentActionRequiredEmailNotification>(subscriptions, renewal, x => x.PaymentUrl = paymentUrl);
    }

    protected virtual async Task OnSoftDeclineAsync(Renewal renewal, Store store, ProcessStoredPaymentRequestResult result)
    {
        var schedule = ParseRetrySchedule(store.Settings.GetValue<string>(ModuleConstants.Settings.Store.RetryScheduleHours));
        var maxRetries = store.Settings.GetValue<int>(ModuleConstants.Settings.Store.MaxRetriesPer30Days);
        var declinesIn30Days = renewal.Events.Count(x => x.EventType == RenewalEventType.PaymentAttempt && x.EventDate >= UtcNow.AddDays(-30));

        var retryIndex = renewal.AttemptCount - 1;
        if (result.IsProviderRetryScheduled)
        {
            // The provider retries on its own (e.g. Adyen Auto Rescue) and the platform must not: wait for the result
            // to arrive as a payment change on the order. No next attempt date marks this wait (see IsProviderRetry).
            renewal.NextAttemptDate = null;
            renewal.LastErrorMessage = "Your card was declined. Your payment provider will try again automatically.";
            renewal.ChangeState(RenewalState.RetryScheduled, "The payment provider retries this payment", UtcNow.Add(_providerRetryWait));
            return;
        }

        if (retryIndex >= schedule.Count || declinesIn30Days >= maxRetries)
        {
            await FailAsync(renewal, "We couldn't charge your card after several attempts. Please update your payment method.", markCardForUpdate: false);
            return;
        }

        var delay = TimeSpan.FromHours(schedule[retryIndex]);
        if (result.RetryAfter != null && result.RetryAfter > delay)
        {
            delay = result.RetryAfter.Value;
        }

        renewal.NextAttemptDate = UtcNow.Add(delay);
        renewal.LastErrorMessage = "Your card was declined. We'll try again automatically.";
        renewal.ChangeState(RenewalState.RetryScheduled, $"Temporary decline; next attempt {renewal.NextAttemptDate:yyyy-MM-dd HH:mm} UTC", renewal.NextAttemptDate);

        if (renewal.AttemptCount == 1)
        {
            var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
            await notificationService.SendAsync<RenewalPaymentFailedEmailNotification>(subscriptions, renewal, x => x.WillRetry = true);
        }
    }

    protected virtual Task OnProviderErrorAsync(Renewal renewal)
    {
        var providerErrors = renewal.Events.Count(x => x.EventType == RenewalEventType.PaymentAttempt && x.Message.EndsWith(nameof(ChargeOutcome.ProviderError)));
        if (providerErrors >= MaxProviderErrors)
        {
            return FailAsync(renewal, "We couldn't reach the payment provider. Please try again later.", markCardForUpdate: false);
        }

        // Retry the same charge with the same idempotency key, so a request that did go through is never charged twice.
        renewal.AttemptCount--;
        renewal.NextAttemptDate = UtcNow.Add(_providerErrorRetryDelay);
        renewal.ChangeState(RenewalState.RetryScheduled, "Payment provider error; retrying the same charge", renewal.NextAttemptDate);
        return Task.CompletedTask;
    }

    /// <summary>The provider's retry window is over and no result reached us: settle on the payment's current status.</summary>
    protected virtual async Task EndProviderRetryAsync(Renewal renewal)
    {
        var order = await customerOrderService.GetByIdAsync(renewal.OrderId);
        var payment = order?.InPayments?.FirstOrDefault(x => x.Id == renewal.PaymentId) ?? order?.InPayments?.FirstOrDefault();
        if (payment != null && _paidStatuses.Contains(payment.PaymentStatus))
        {
            renewal.AuthorizedDate ??= UtcNow;
            renewal.AuthorizationExpiresDate ??= UtcNow.AddMinutes(await GetAuthorizationLifetimeMinutesAsync());
            renewal.LastErrorMessage = null;
            renewal.ChangeState(payment.PaymentStatus == PaymentStatus.Paid ? RenewalState.Completed : RenewalState.Authorized, "The payment provider collected the payment");
            await ReactivateSubscriptionsAsync(renewal);
            return;
        }

        await FailAsync(renewal, "We couldn't charge your card. Please update your payment method.", markCardForUpdate: false);
    }

    protected virtual async Task FailAsync(Renewal renewal, string customerMessage, bool markCardForUpdate)
    {
        renewal.LastErrorMessage = customerMessage;
        renewal.NextAttemptDate = null;
        renewal.ChangeState(RenewalState.Failed, customerMessage);

        var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
        var toPastDue = subscriptions.Where(x => x.Status == SubscriptionStatus.Active).ToList();
        foreach (var subscription in toPastDue)
        {
            subscription.Status = SubscriptionStatus.PastDue;
        }

        if (toPastDue.Count > 0)
        {
            await subscriptionService.SaveChangesAsync(toPastDue);
        }

        await notificationService.SendAsync<RenewalPaymentFailedEmailNotification>(subscriptions, renewal, x => x.WillRetry = false);
    }

    protected virtual async Task CaptureAsync(Renewal renewal, CustomerOrder order, PaymentIn payment)
    {
        if (renewal.AuthorizationExpiresDate != null && renewal.AuthorizationExpiresDate <= UtcNow)
        {
            var reauthorized = await ReauthorizeAsync(renewal, order, payment);
            if (!reauthorized)
            {
                return;
            }
        }

        var result = await paymentFlowService.CapturePaymentAsync(new CaptureOrderPaymentRequest
        {
            OrderId = order.Id,
            PaymentId = payment.Id,
            // Stable per renewal: the Orders module matches captures by it, so a retried capture is never duplicated.
            TransactionId = $"{renewal.IdempotencyKey ?? renewal.Id}-capture",
            Amount = payment.Sum,
            CloseTransaction = true,
        });

        if (result.Succeeded)
        {
            renewal.CapturedDate = UtcNow;
            renewal.AddEvent(RenewalEventType.Captured, $"Captured {payment.Sum:0.00} {order.Currency} after shipment");
            renewal.ChangeState(RenewalState.Completed, "Order shipped; payment captured");
        }
        else
        {
            renewal.AddEvent(RenewalEventType.Error, "Capture failed; will try again.", result.ErrorMessage);
            renewal.NextProcessingDate = UtcNow.AddHours(1);
        }
    }

    protected virtual async Task<bool> ReauthorizeAsync(Renewal renewal, CustomerOrder order, PaymentIn payment)
    {
        var store = await storeService.GetNoCloneAsync(order.StoreId);
        var paymentMethod = await GetPaymentMethodAsync(order.StoreId, payment.GatewayCode);
        var storedMethod = string.IsNullOrEmpty(renewal.StoredPaymentMethodId) ? null : await storedPaymentMethodService.GetByIdAsync(renewal.StoredPaymentMethodId);
        if (paymentMethod is not ISupportStoredCredentialFlow || storedMethod == null)
        {
            return true;
        }

        var reauthCount = renewal.Events.Count(x => x.EventType == RenewalEventType.PaymentAttempt && x.Message.StartsWith("Re-authorization"));
        var result = await paymentMethod.ProcessStoredPaymentAsync(new ProcessStoredPaymentRequest
        {
            OrderId = order.Id,
            Order = order,
            PaymentId = payment.Id,
            Payment = payment,
            StoreId = order.StoreId,
            Store = store,
            StoredPaymentMethod = storedMethod,
            Amount = payment.Sum,
            Currency = order.Currency,
            IdempotencyKey = $"{renewal.IdempotencyKey}-reauth-{reauthCount + 1}",
            Reason = MerchantInitiatedReason.Reauthorization,
            CaptureMode = StoredPaymentCaptureMode.AuthorizeOnly,
        });

        renewal.AddEvent(RenewalEventType.PaymentAttempt, $"Re-authorization: {result.Outcome}",
            JsonConvert.SerializeObject(new { result.RawCode, result.AdviceCode, result.TransactionId }));

        if (result.Outcome != ChargeOutcome.Succeeded)
        {
            await FailAsync(renewal, "Your card was declined when the order shipped. Please update your payment method.", markCardForUpdate: false);
            return false;
        }

        payment.OuterId = result.TransactionId;
        payment.AuthorizedDate = UtcNow;
        await customerOrderService.SaveChangesAsync([order]);

        renewal.AuthorizedDate = UtcNow;
        renewal.AuthorizationExpiresDate = UtcNow.AddMinutes(await GetAuthorizationLifetimeMinutesAsync());
        return true;
    }

    #endregion

    #region Helpers

    protected virtual async Task<IList<Subscription>> LoadSubscriptionsAsync(Renewal renewal, bool onlyActive)
    {
        var ids = renewal.Items.Select(x => x.SubscriptionId).Distinct().ToList();
        var subscriptions = await subscriptionService.GetAsync(ids);
        return onlyActive ? subscriptions.Where(x => x.Status == SubscriptionStatus.Active).ToList() : subscriptions.ToList();
    }

    protected virtual void RemoveInactiveItems(Renewal renewal, IList<Subscription> activeSubscriptions)
    {
        var activeIds = activeSubscriptions.Select(x => x.Id).ToHashSet();
        foreach (var item in renewal.Items.Where(x => !activeIds.Contains(x.SubscriptionId)).ToList())
        {
            renewal.Items.Remove(item);
            renewal.AddEvent(RenewalEventType.StateChanged, $"{item.ProductName} removed: the subscription is no longer active");
        }
    }

    protected virtual void ApplyCartResult(Renewal renewal, RenewalCartResult cart)
    {
        renewal.Items = cart.Items;
        renewal.DiscountTotal = cart.DiscountTotal;
        renewal.ShippingTotal = cart.ShippingTotal;
        renewal.TaxTotal = cart.TaxTotal;
        renewal.PreviewTotal = cart.Total;
    }

    protected virtual async Task AdvanceSubscriptionsAsync(Renewal renewal, IList<Subscription> subscriptions, CustomerOrder order)
    {
        var now = UtcNow;
        foreach (var subscription in subscriptions)
        {
            var included = renewal.Items.Any(x => x.SubscriptionId == subscription.Id && !x.IsExcluded);
            if (order != null && included)
            {
                subscription.LastOrderId = order.Id;
                subscription.LastOrderNumber = order.Number;
                subscription.LastOrderDate = now;
                subscription.OrdersCount++;
            }

            if (renewal.IsManual)
            {
                // "Order now" restarts the schedule from today.
                subscription.AnchorDay = now.Day;
                subscription.NextOrderDate = SubscriptionSchedule.AddInterval(now, subscription.IntervalUnit, subscription.IntervalCount, subscription.AnchorDay);
            }
            else
            {
                subscription.NextOrderDate = SubscriptionSchedule.NextAfter(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount, now, subscription.EffectiveAnchorDay);
            }

            if (subscription.CurrentRenewalId == renewal.Id)
            {
                subscription.CurrentRenewalId = null;
            }
        }

        if (subscriptions.Count > 0)
        {
            await subscriptionService.SaveChangesAsync(subscriptions);
        }
    }

    protected virtual async Task ReactivateSubscriptionsAsync(Renewal renewal)
    {
        var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
        var pastDue = subscriptions.Where(x => x.Status == SubscriptionStatus.PastDue).ToList();
        foreach (var subscription in pastDue)
        {
            subscription.Status = SubscriptionStatus.Active;
        }

        if (pastDue.Count > 0)
        {
            await subscriptionService.SaveChangesAsync(pastDue);
        }
    }

    protected virtual async Task<PaymentMethod> GetPaymentMethodAsync(string storeId, string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PaymentMethodsSearchCriteria>.TryCreateInstance();
        criteria.StoreId = storeId;
        criteria.Codes = [code];
        criteria.Take = 1;
        return (await paymentMethodsSearchService.SearchAsync(criteria)).Results.FirstOrDefault();
    }

    protected virtual async Task<int> GetAuthorizationLifetimeMinutesAsync()
    {
        var minutes = await settingsManager.GetValueAsync<int>(ModuleConstants.Settings.General.AuthorizationLifetimeMinutes);
        return minutes > 0 ? minutes : 7 * 24 * 60;
    }

    protected static bool IsShipped(CustomerOrder order)
    {
        return order?.Shipments?.Any(x => x.Status is "Sent" or "Completed") == true
            || order?.Status is "Completed" or "Sent";
    }

    protected static IList<int> ParseRetrySchedule(string value)
    {
        var result = (value ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) ? hours : -1)
            .Where(x => x > 0)
            .ToList();

        return result.Count > 0 ? result : [24, 72, 120];
    }

    /// <summary>A retry the payment provider runs on its own: the only retry state without a next attempt date.</summary>
    protected static bool IsProviderRetry(Renewal renewal)
    {
        return renewal.State == RenewalState.RetryScheduled && renewal.NextAttemptDate == null;
    }

    /// <summary>Clears the link from subscriptions to a renewal that has ended, so the scheduler plans their next delivery.</summary>
    protected virtual async Task ReleaseSubscriptionsAsync(Renewal renewal)
    {
        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.CurrentRenewalId = renewal.Id;
        criteria.Take = SchedulerPageSize;

        var linked = (await subscriptionSearchService.SearchAsync(criteria)).Results;
        if (linked.Count == 0)
        {
            return;
        }

        foreach (var subscription in linked)
        {
            subscription.CurrentRenewalId = null;
        }

        await subscriptionService.SaveChangesAsync(linked);
    }

    /// <summary>Runs <paramref name="action"/> on a fresh copy of the renewal, under its customer's lock.</summary>
    protected virtual async Task<Renewal> WithLockAsync(string renewalId, Func<Renewal, Task<Renewal>> action)
    {
        var customerId = (await renewalService.GetNoCloneAsync(renewalId))?.CustomerId
            ?? throw new SubscriptionOperationException("Renewal not found.");

        return await distributedLock.RunForCustomerAsync(customerId, async () =>
        {
            var renewal = await renewalService.GetByIdAsync(renewalId)
                ?? throw new SubscriptionOperationException("Renewal not found.");

            var result = await action(renewal);
            if (result is { IsOpen: false })
            {
                await ReleaseSubscriptionsAsync(result);
            }

            return result;
        });
    }

    #endregion
}
