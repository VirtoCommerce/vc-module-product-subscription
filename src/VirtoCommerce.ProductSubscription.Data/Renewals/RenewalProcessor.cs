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
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Renewals;

/// <summary>
/// The renewal state machine. Every step is safe to run twice: order placement is guarded by the stored order id,
/// charges by an idempotency key per attempt, and each renewal is processed under a distributed lock.
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
    ISettingsManager settingsManager,
    ISubscriptionNotificationService notificationService,
    IDistributedLock distributedLock,
    ILogger<RenewalProcessor> logger)
    : IRenewalProcessor
{
    private const int MaxSteps = 12;
    private const int MaxProviderErrors = 5;
    private static readonly TimeSpan _providerErrorRetryDelay = TimeSpan.FromMinutes(15);
    private static readonly PaymentStatus[] _paidStatuses = [PaymentStatus.Authorized, PaymentStatus.Paid];

    protected virtual DateTime UtcNow => DateTime.UtcNow;

    #region Scheduler

    public virtual async Task<int> ProcessDueAsync()
    {
        var now = UtcNow;

        await ResumePausedSubscriptionsAsync(now);
        await CreateDueRenewalsAsync(now);

        var processed = 0;
        var criteria = AbstractTypeFactory<RenewalSearchCriteria>.TryCreateInstance();
        criteria.DueBefore = now;
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
        criteria.Take = 500;
        var toResume = (await subscriptionSearchService.SearchAsync(criteria)).Results
            .Where(x => x.PausedUntil != null && x.PausedUntil <= now)
            .ToList();

        foreach (var subscription in toResume)
        {
            subscription.Status = SubscriptionStatus.Active;
            subscription.PausedUntil = null;
            if (subscription.NextOrderDate < now)
            {
                subscription.NextOrderDate = now.AddDays(1);
            }
        }

        if (toResume.Count > 0)
        {
            await subscriptionService.SaveChangesAsync(toResume);
        }
    }

    protected virtual async Task CreateDueRenewalsAsync(DateTime now)
    {
        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.Statuses = [SubscriptionStatus.Active];
        criteria.WithoutOpenRenewal = true;
        criteria.NextOrderDateBefore = now.AddDays(60);
        criteria.Take = 500;

        var candidates = (await subscriptionSearchService.SearchAsync(criteria)).Results;
        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var storeGroup in candidates.GroupBy(x => x.StoreId))
        {
            var store = await storeService.GetNoCloneAsync(storeGroup.Key);
            if (store == null)
            {
                continue;
            }

            var leadDays = store.Settings.GetValue<int>(ModuleConstants.Settings.Store.ReminderLeadDays);
            var due = storeGroup.Where(x => x.NextOrderDate.AddDays(-leadDays) <= now).ToList();

            foreach (var group in due.GroupBy(GetGroupKey))
            {
                try
                {
                    await CreateRenewalAsync(group.ToList(), store, isManual: false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create a renewal for subscriptions {Numbers}", string.Join(", ", group.Select(x => x.Number)));
                }
            }
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
        renewal.Items = subscriptions.Select(x => new RenewalItem
        {
            SubscriptionId = x.Id,
            ProductId = x.ProductId,
            Sku = x.Sku,
            ProductName = x.ProductName,
            ImageUrl = x.ImageUrl,
            Quantity = x.Quantity,
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
        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.Statuses = [SubscriptionStatus.Active, SubscriptionStatus.Paused];
        criteria.Take = 200;
        var subscriptions = (await subscriptionSearchService.SearchAsync(criteria)).Results
            .Where(x => (x.LastAnnualReminderDate ?? x.StartDate) <= now.AddYears(-1))
            .ToList();

        foreach (var customerGroup in subscriptions.GroupBy(x => (x.StoreId, x.CustomerId)))
        {
            var store = await storeService.GetNoCloneAsync(customerGroup.Key.StoreId);
            if (store?.Settings.GetValue<bool>(ModuleConstants.Settings.Store.AnnualReminderEnabled) != true)
            {
                continue;
            }

            var group = customerGroup.ToList();
            await notificationService.SendAsync<SubscriptionAnnualReminderEmailNotification>(group);
            foreach (var subscription in group)
            {
                subscription.LastAnnualReminderDate = now;
            }

            await subscriptionService.SaveChangesAsync(group);
        }
    }

    #endregion

    #region Commands

    public virtual async Task<Renewal> ProcessAsync(string renewalId, bool ignoreSchedule = false)
    {
        Renewal result = null;
        var ran = await distributedLock.TryExecuteAsync(GetLockResource(renewalId),
            async _ => { result = await ProcessLockedAsync(renewalId, ignoreSchedule); },
            TimeSpan.FromSeconds(30));

        return ran ? result : await renewalService.GetByIdAsync(renewalId);
    }

    public virtual async Task<Renewal> OrderNowAsync(string subscriptionId)
    {
        var subscription = await subscriptionService.GetByIdAsync(subscriptionId)
            ?? throw new InvalidOperationException("Subscription not found.");

        if (subscription.Status != SubscriptionStatus.Active)
        {
            throw new InvalidOperationException("Only active subscriptions can be ordered now.");
        }

        if (!string.IsNullOrEmpty(subscription.CurrentRenewalId))
        {
            var open = await renewalService.GetByIdAsync(subscription.CurrentRenewalId);
            if (open != null && open.IsOpen)
            {
                if (!RenewalState.BeforeOrder.Contains(open.State))
                {
                    throw new InvalidOperationException("An order for this subscription is already in progress.");
                }

                open.IsManual = true;
                open.CutoffDate = UtcNow;
                open.NextProcessingDate = UtcNow;
                open.AddEvent(RenewalEventType.CustomerAction, "Order now requested");
                await renewalService.SaveChangesAsync([open]);
                return await ProcessAsync(open.Id, ignoreSchedule: true);
            }
        }

        var store = await storeService.GetNoCloneAsync(subscription.StoreId);
        var renewal = await CreateRenewalAsync([subscription], store, isManual: true);
        return await ProcessAsync(renewal.Id, ignoreSchedule: true);
    }

    public virtual async Task<Renewal> AcceptReviewAsync(string renewalId)
    {
        await WithLockAsync(renewalId, async renewal =>
        {
            if (renewal.State != RenewalState.ReviewRequired)
            {
                throw new InvalidOperationException("This renewal doesn't need a review.");
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
        });

        return await ProcessAsync(renewalId, ignoreSchedule: true);
    }

    public virtual async Task<Renewal> SkipAsync(string renewalId)
    {
        return await WithLockAsync(renewalId, async renewal =>
        {
            if (!RenewalState.BeforeOrder.Contains(renewal.State))
            {
                throw new InvalidOperationException("This renewal can no longer be skipped: the order has been placed.");
            }

            var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
            foreach (var subscription in subscriptions)
            {
                subscription.NextOrderDate = SubscriptionSchedule.NextAfter(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount, UtcNow);
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
        });
    }

    public virtual async Task<Renewal> CancelAsync(string renewalId, string reason)
    {
        return await WithLockAsync(renewalId, async renewal =>
        {
            if (!RenewalState.BeforeOrder.Contains(renewal.State))
            {
                throw new InvalidOperationException("This renewal can no longer be cancelled: the order has been placed.");
            }

            var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
            foreach (var subscription in subscriptions)
            {
                subscription.NextOrderDate = SubscriptionSchedule.NextAfter(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount, UtcNow);
                if (subscription.CurrentRenewalId == renewal.Id)
                {
                    subscription.CurrentRenewalId = null;
                }
            }

            await subscriptionService.SaveChangesAsync(subscriptions);

            renewal.ChangeState(RenewalState.Cancelled, string.IsNullOrWhiteSpace(reason) ? "Cancelled by an administrator" : reason);
            await renewalService.SaveChangesAsync([renewal]);
        });
    }

    public virtual async Task<Renewal> RetryPaymentAsync(string renewalId)
    {
        await WithLockAsync(renewalId, async renewal =>
        {
            if (renewal.State is not (RenewalState.Failed or RenewalState.RetryScheduled or RenewalState.PaymentActionRequired))
            {
                throw new InvalidOperationException("There is no failed payment to retry for this renewal.");
            }

            if (string.IsNullOrEmpty(renewal.OrderId))
            {
                throw new InvalidOperationException("The renewal has no order to pay.");
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
        });

        return await ProcessAsync(renewalId, ignoreSchedule: true);
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
        var renewalId = (await renewalSearchService.SearchAsync(criteria, clone: false)).Results.FirstOrDefault()?.Id;
        if (renewalId == null)
        {
            return;
        }

        // No wait: if the renewal is being processed right now (e.g. this change comes from our own capture), skip.
        await distributedLock.TryExecuteAsync(GetLockResource(renewalId), async _ =>
        {
            var renewal = await renewalService.GetByIdAsync(renewalId);
            if (renewal == null)
            {
                return;
            }

            var freshOrder = await customerOrderService.GetByIdAsync(order.Id);
            var payment = freshOrder?.InPayments?.FirstOrDefault(x => x.Id == renewal.PaymentId) ?? freshOrder?.InPayments?.FirstOrDefault();
            if (payment == null)
            {
                return;
            }

            var changed = false;

            if (renewal.State is RenewalState.PaymentActionRequired or RenewalState.RetryScheduled or RenewalState.Failed or RenewalState.Ordered
                && _paidStatuses.Contains(payment.PaymentStatus))
            {
                renewal.AuthorizedDate = UtcNow;
                renewal.AuthorizationExpiresDate = UtcNow.AddMinutes(await GetAuthorizationLifetimeMinutesAsync());
                renewal.LastErrorMessage = null;
                renewal.ChangeState(payment.PaymentStatus == PaymentStatus.Paid ? RenewalState.Completed : RenewalState.Authorized, "Payment completed by the customer");
                await ReactivateSubscriptionsAsync(renewal);
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
            }
        });
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

        if (paymentMethod is not ISupportStoredCredentialFlow || storedMethod == null)
        {
            renewal.ChangeState(RenewalState.Completed, "Order placed. This payment method doesn't support automatic charging; payment is collected separately.");
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
                await OnRequiresActionAsync(renewal, store, order);
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

    protected virtual async Task OnRequiresActionAsync(Renewal renewal, Store store, CustomerOrder order)
    {
        var waitDays = store.Settings.GetValue<int>(ModuleConstants.Settings.Store.PaymentActionWaitDays);
        renewal.LastErrorMessage = "Your bank asked you to confirm this payment.";
        renewal.ChangeState(RenewalState.PaymentActionRequired, "The bank requires authentication (3-D Secure)", UtcNow.AddDays(Math.Max(1, waitDays)));

        var subscriptions = await LoadSubscriptionsAsync(renewal, onlyActive: false);
        var storeUrl = await notificationService.GetStoreUrlAsync(renewal.StoreId);
        var paymentUrl = $"{storeUrl}account/orders/{order.Id}/payment";
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
            renewal.ChangeState(RenewalState.RetryScheduled, "The payment provider retries this payment", UtcNow.AddDays(2));
            renewal.NextAttemptDate = renewal.NextProcessingDate;
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

            subscription.NextOrderDate = renewal.IsManual
                ? SubscriptionSchedule.AddInterval(now, subscription.IntervalUnit, subscription.IntervalCount)
                : SubscriptionSchedule.NextAfter(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount, now);

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
        var minutes = await settingsManager.GetValueAsync<int>(ModuleConstants.Settings.General.MockAuthorizationLifetimeMinutes);
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

    protected static string GetLockResource(string renewalId) => $"product-subscription:renewal:{renewalId}";

    protected virtual async Task<Renewal> WithLockAsync(string renewalId, Func<Renewal, Task> action)
    {
        Renewal result = null;
        var ran = await distributedLock.TryExecuteAsync(GetLockResource(renewalId), async _ =>
        {
            var renewal = await renewalService.GetByIdAsync(renewalId)
                ?? throw new InvalidOperationException("Renewal not found.");
            await action(renewal);
            result = renewal;
        }, TimeSpan.FromSeconds(30));

        if (!ran)
        {
            throw new InvalidOperationException("The renewal is being processed right now. Please try again in a moment.");
        }

        return result;
    }

    #endregion
}
