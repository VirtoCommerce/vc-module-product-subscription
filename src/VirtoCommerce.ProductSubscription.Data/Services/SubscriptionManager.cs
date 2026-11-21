using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;
using Address = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.ProductSubscription.Data.Services;

[SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters",
    Justification = "A DI constructor: each parameter is a platform or module service the actions use.")]
public class SubscriptionManager(
    ISubscriptionService subscriptionService,
    ISubscriptionSearchService subscriptionSearchService,
    ISubscriptionPlanService planService,
    ISubscriptionCartItemService cartItemService,
    ISubscriptionCartItemSearchService cartItemSearchService,
    IRenewalService renewalService,
    IStoredPaymentMethodService storedPaymentMethodService,
    IStoredPaymentMethodSearchService storedPaymentMethodSearchService,
    IPaymentConsentSearchService paymentConsentSearchService,
    ISubscriptionNotificationService notificationService,
    IItemService itemService,
    IStoreService storeService,
    IDistributedLock distributedLock,
    ILogger<SubscriptionManager> logger)
    : ISubscriptionManager
{
    private const int MaxQuantity = 9999;

    private static readonly PaymentStatus[] _paidStatuses = [PaymentStatus.Authorized, PaymentStatus.Paid];

    public virtual async Task<IList<Subscription>> CreateFromOrderAsync(CustomerOrder order)
    {
        if (!IsCheckoutOrder(order))
        {
            return [];
        }

        return await CreateFromOrderAsync(order, await GetCartChoicesAsync(order.ShoppingCartId));
    }

    public virtual async Task<IList<Subscription>> CreateFromOrderAsync(CustomerOrder order, IList<SubscriptionCartItem> choices)
    {
        if (!IsCheckoutOrder(order) || choices.IsNullOrEmpty())
        {
            return [];
        }

        choices = choices.Where(x => x.CartId == order.ShoppingCartId).ToList();
        if (choices.Count == 0 || await HasSubscriptionsAsync(order))
        {
            return [];
        }

        var plans = (await planService.GetAsync(choices.Select(x => x.PlanId).Distinct().ToList())).ToDictionary(x => x.Id);
        var defaults = await GetOrderDefaultsAsync(order);
        var subscriptions = new List<Subscription>();
        var usedChoices = new List<SubscriptionCartItem>();
        var matches = MatchOrderLines(choices, order.Items.ToList());

        foreach (var choice in choices)
        {
            if (matches.TryGetValue(choice, out var lineItem) && plans.TryGetValue(choice.PlanId, out var plan))
            {
                subscriptions.Add(CreateSubscription(order, lineItem, plan, choice, defaults));
                usedChoices.Add(choice);
            }
        }

        if (subscriptions.Count == 0)
        {
            return [];
        }

        await subscriptionService.SaveChangesAsync(subscriptions);
        await cartItemService.DeleteAsync(usedChoices.Select(x => x.Id).ToList());

        logger.LogInformation("Created {Count} subscription(s) from order {OrderNumber}", subscriptions.Count, order.Number);

        await notificationService.SendAsync<SubscriptionCreatedEmailNotification>(subscriptions);

        return subscriptions;
    }

    public virtual async Task<IList<SubscriptionCartItem>> GetCartChoicesAsync(string cartId)
    {
        if (string.IsNullOrEmpty(cartId))
        {
            return [];
        }

        var choicesCriteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        choicesCriteria.CartIds = [cartId];
        choicesCriteria.Take = ModuleConstants.SearchPageSize;
        return await cartItemSearchService.SearchAllAsync(choicesCriteria);
    }

    /// <summary>A placed order made from a cart, with lines: the only kind subscriptions are created from.</summary>
    protected static bool IsCheckoutOrder(CustomerOrder order)
    {
        return order is { IsPrototype: false } && !string.IsNullOrEmpty(order.ShoppingCartId) && !order.Items.IsNullOrEmpty();
    }

    /// <summary>Idempotent: never create subscriptions twice for the same order.</summary>
    protected virtual async Task<bool> HasSubscriptionsAsync(CustomerOrder order)
    {
        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.InitialOrderId = order.Id;
        criteria.Take = 1;
        return (await subscriptionSearchService.SearchAsync(criteria)).TotalCount > 0;
    }

    /// <summary>What every subscription of an order shares: delivery, payment, consent and dates.</summary>
    protected virtual async Task<OrderSubscriptionDefaults> GetOrderDefaultsAsync(CustomerOrder order)
    {
        var now = DateTime.UtcNow;
        var defaults = new OrderSubscriptionDefaults
        {
            Now = now,
            PlacedDate = order.CreatedDate == default ? now : order.CreatedDate,
            ConsentId = await FindConsentIdAsync(order),
            IsPaymentAuthorized = order.InPayments?.Any(x => _paidStatuses.Contains(x.PaymentStatus)) == true,
        };

        SetDelivery(defaults, order);
        SetPayment(defaults, order, await FindStoredPaymentMethodAsync(order));

        return defaults;
    }

    /// <summary>The delivery of the order's first shipment; the address falls back to the order's addresses.</summary>
    protected virtual void SetDelivery(OrderSubscriptionDefaults defaults, CustomerOrder order)
    {
        var shipment = order.Shipments?.FirstOrDefault();
        defaults.ShippingMethodCode = shipment?.ShipmentMethodCode;
        defaults.ShippingMethodOption = shipment?.ShipmentMethodOption;
        defaults.ShippingAddress = ToCoreAddress(shipment?.DeliveryAddress) ?? ToCoreAddress(GetOrderShippingAddress(order));
        defaults.CustomerEmail = defaults.ShippingAddress?.Email ?? order.Addresses?.Select(x => x.Email).FirstOrDefault(x => !string.IsNullOrEmpty(x));
    }

    protected static Address GetOrderShippingAddress(CustomerOrder order)
    {
        var addresses = order.Addresses ?? [];
        return addresses.FirstOrDefault(x => x.AddressType is AddressType.Shipping or AddressType.BillingAndShipping) ?? addresses.FirstOrDefault();
    }

    /// <summary>The saved card the order paid with, or the order's payment method.</summary>
    protected virtual void SetPayment(OrderSubscriptionDefaults defaults, CustomerOrder order, StoredPaymentMethod storedPaymentMethod)
    {
        defaults.StoredPaymentMethodId = storedPaymentMethod?.Id;
        defaults.PaymentMethodCode = storedPaymentMethod?.PaymentMethodCode ?? order.InPayments?.FirstOrDefault()?.GatewayCode;
    }

    /// <summary>The subscription a cart choice becomes, from its order line and plan.</summary>
    protected virtual Subscription CreateSubscription(CustomerOrder order, LineItem lineItem, SubscriptionPlan plan, SubscriptionCartItem choice, OrderSubscriptionDefaults defaults)
    {
        var configuration = SubscriptionConfigurationMapper.FromOrderLine(lineItem, order.Id, order.Number);

        var subscription = AbstractTypeFactory<Subscription>.TryCreateInstance();
        subscription.StoreId = order.StoreId;
        subscription.CustomerId = order.CustomerId;
        subscription.CustomerName = order.CustomerName;
        subscription.CustomerEmail = defaults.CustomerEmail;
        subscription.OrganizationId = order.OrganizationId;
        subscription.ProductId = lineItem.ProductId;
        subscription.Sku = lineItem.Sku;
        subscription.ProductName = lineItem.Name;
        subscription.ImageUrl = lineItem.ImageUrl;
        subscription.Quantity = Math.Max(1, lineItem.Quantity);
        subscription.Configuration = configuration;
        subscription.ConfigurationHash = configuration?.ComputeHash();
        subscription.PlanId = plan.Id;
        subscription.PlanName = plan.Name;
        subscription.IntervalUnit = choice.IntervalUnit;
        subscription.IntervalCount = Math.Max(1, choice.IntervalCount);
        subscription.DiscountType = plan.DiscountType;
        subscription.DiscountValue = plan.DiscountValue;
        subscription.Status = defaults.IsPaymentAuthorized ? SubscriptionStatus.Active : SubscriptionStatus.PendingActivation;
        subscription.StartDate = defaults.Now;
        subscription.AnchorDay = defaults.PlacedDate.Day;
        subscription.NextOrderDate = SubscriptionSchedule.AddInterval(defaults.PlacedDate, subscription.IntervalUnit, subscription.IntervalCount, subscription.AnchorDay);
        subscription.Currency = order.Currency;
        subscription.LanguageCode = order.LanguageCode;
        subscription.ShippingAddress = (Address)defaults.ShippingAddress?.Clone();
        subscription.ShippingMethodCode = defaults.ShippingMethodCode;
        subscription.ShippingMethodOption = defaults.ShippingMethodOption;
        subscription.PaymentMethodCode = defaults.PaymentMethodCode;
        subscription.StoredPaymentMethodId = defaults.StoredPaymentMethodId;
        subscription.ConsentId = defaults.ConsentId;
        subscription.InitialOrderId = order.Id;
        subscription.InitialOrderNumber = order.Number;
        subscription.LastOrderId = order.Id;
        subscription.LastOrderNumber = order.Number;
        subscription.LastOrderDate = defaults.Now;
        subscription.OrdersCount = 1;
        subscription.ReferencePrice = lineItem.PlacedPrice;
        subscription.LastAnnualReminderDate = defaults.Now;

        return subscription;
    }

    /// <summary>The values every subscription created from one order shares.</summary>
    protected class OrderSubscriptionDefaults
    {
        public DateTime Now { get; set; }
        public DateTime PlacedDate { get; set; }
        public Address ShippingAddress { get; set; }
        public string CustomerEmail { get; set; }
        public string ShippingMethodCode { get; set; }
        public string ShippingMethodOption { get; set; }
        public string PaymentMethodCode { get; set; }
        public string StoredPaymentMethodId { get; set; }
        public string ConsentId { get; set; }
        public bool IsPaymentAuthorized { get; set; }
    }

    /// <summary>
    /// The order line each cart choice became. Order lines don't keep the cart line id, so a choice is matched by product
    /// and configuration fingerprint; each line backs one choice, so two lines of one product keep their own quantity,
    /// price and configuration. Exact matches are taken for every choice first: a choice whose fingerprint is out of date
    /// then falls back to a line no other choice matched exactly, never to another choice's line.
    /// </summary>
    protected virtual IDictionary<SubscriptionCartItem, LineItem> MatchOrderLines(IList<SubscriptionCartItem> choices, IList<LineItem> lines)
    {
        var result = new Dictionary<SubscriptionCartItem, LineItem>();
        var unmatched = lines.ToList();
        var hashes = lines.ToDictionary(x => x, SubscriptionConfigurationMapper.GetHash);

        foreach (var choice in choices)
        {
            var line = unmatched.FirstOrDefault(x => x.ProductId == choice.ProductId && hashes[x] == choice.ConfigurationHash);
            if (line != null)
            {
                result[choice] = line;
                unmatched.Remove(line);
            }
        }

        foreach (var choice in choices.Where(x => !result.ContainsKey(x)))
        {
            var sameProduct = unmatched.Where(x => x.ProductId == choice.ProductId).ToList();
            var line = sameProduct.FirstOrDefault(x => x.IsConfigured == !string.IsNullOrEmpty(choice.ConfigurationHash))
                ?? sameProduct.FirstOrDefault();
            if (line != null)
            {
                result[choice] = line;
                unmatched.Remove(line);
            }
        }

        return result;
    }

    public virtual async Task<IList<Subscription>> ActivateForOrderAsync(CustomerOrder order)
    {
        if (order?.InPayments?.Any(x => _paidStatuses.Contains(x.PaymentStatus)) != true)
        {
            return [];
        }

        return await distributedLock.RunForCustomerAsync(order.CustomerId, async () =>
        {
            var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
            criteria.InitialOrderId = order.Id;
            criteria.Statuses = [SubscriptionStatus.PendingActivation];
            criteria.Take = ModuleConstants.SearchPageSize;
            var pending = await subscriptionSearchService.SearchAllAsync(criteria);
            if (pending.Count == 0)
            {
                return pending;
            }

            var storedPaymentMethod = await FindStoredPaymentMethodAsync(order);
            foreach (var subscription in pending)
            {
                subscription.Status = SubscriptionStatus.Active;
                if (storedPaymentMethod != null)
                {
                    subscription.StoredPaymentMethodId = storedPaymentMethod.Id;
                    subscription.PaymentMethodCode = storedPaymentMethod.PaymentMethodCode;
                }
            }

            await subscriptionService.SaveChangesAsync(pending);

            return pending;
        });
    }

    public virtual async Task<IList<Subscription>> LinkStoredPaymentMethodAsync(StoredPaymentMethod storedPaymentMethod)
    {
        if (storedPaymentMethod == null
            || string.IsNullOrEmpty(storedPaymentMethod.InitialOrderId)
            || string.IsNullOrEmpty(storedPaymentMethod.CustomerId)
            || storedPaymentMethod.Status != StoredPaymentMethodStatus.Active)
        {
            return [];
        }

        return await distributedLock.RunForCustomerAsync(storedPaymentMethod.CustomerId, async () =>
        {
            var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
            criteria.InitialOrderId = storedPaymentMethod.InitialOrderId;
            criteria.CustomerId = storedPaymentMethod.CustomerId;
            criteria.Statuses = [SubscriptionStatus.PendingActivation, SubscriptionStatus.Active, SubscriptionStatus.Paused, SubscriptionStatus.PastDue];
            criteria.Take = ModuleConstants.SearchPageSize;
            var toLink = (await subscriptionSearchService.SearchAllAsync(criteria))
                .Where(x => x.StoredPaymentMethodId != storedPaymentMethod.Id)
                .ToList();
            if (toLink.Count == 0)
            {
                return toLink;
            }

            foreach (var subscription in toLink)
            {
                subscription.StoredPaymentMethodId = storedPaymentMethod.Id;
                subscription.PaymentMethodCode = storedPaymentMethod.PaymentMethodCode;
            }

            await subscriptionService.SaveChangesAsync(toLink);

            return toLink;
        });
    }

    public virtual Task<Subscription> SkipNextAsync(string subscriptionId, DateTime? expectedNextOrderDate = null)
    {
        return WithLockAsync(subscriptionId, async subscription =>
        {
            EnsureStatus(subscription, SubscriptionStatus.Active, SubscriptionStatus.PastDue);

            // The page the customer acted on may be stale: a minute of slack absorbs date serialization, while a real
            // move (a skip, a change) is at least a day.
            if (expectedNextOrderDate != null
                && Math.Abs((expectedNextOrderDate.Value.ToUniversalTime() - DateTime.SpecifyKind(subscription.NextOrderDate, DateTimeKind.Utc)).TotalMinutes) >= 1)
            {
                throw new SubscriptionOperationException("The next delivery has changed since this page was opened. Refresh the page and try again.");
            }

            await DetachFromOpenRenewalAsync(subscription, "Skipped by customer");

            var skippedDate = subscription.NextOrderDate;
            subscription.NextOrderDate = SubscriptionSchedule.AddInterval(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount, subscription.EffectiveAnchorDay);
            subscription.SkipCount++;
            await subscriptionService.SaveChangesAsync([subscription]);

            await notificationService.SendAsync<DeliverySkippedEmailNotification>([subscription], configure: x => x.SkippedDate = skippedDate);

            return subscription;
        });
    }

    public virtual Task<Subscription> PauseAsync(string subscriptionId, DateTime? resumeDate)
    {
        return WithLockAsync(subscriptionId, async subscription =>
        {
            EnsureStatus(subscription, SubscriptionStatus.Active, SubscriptionStatus.PastDue);

            if (resumeDate != null && resumeDate.Value.ToUniversalTime() <= DateTime.UtcNow)
            {
                throw new SubscriptionOperationException("The resume date must be in the future.");
            }

            await DetachFromOpenRenewalAsync(subscription, "Subscription paused");

            subscription.Status = SubscriptionStatus.Paused;
            subscription.PausedUntil = resumeDate?.ToUniversalTime();
            await subscriptionService.SaveChangesAsync([subscription]);

            await notificationService.SendAsync<SubscriptionPausedEmailNotification>([subscription]);

            return subscription;
        });
    }

    public virtual Task<Subscription> ResumeAsync(string subscriptionId)
    {
        return WithLockAsync(subscriptionId, async subscription =>
        {
            EnsureStatus(subscription, SubscriptionStatus.Paused);

            subscription.Status = SubscriptionStatus.Active;
            subscription.PausedUntil = null;

            var earliest = await GetEarliestNextOrderDateAsync(subscription);
            if (subscription.NextOrderDate < earliest)
            {
                subscription.NextOrderDate = earliest;
            }

            await subscriptionService.SaveChangesAsync([subscription]);

            await notificationService.SendAsync<SubscriptionResumedEmailNotification>([subscription]);

            return subscription;
        });
    }

    public virtual Task<Subscription> CancelAsync(string subscriptionId, string reason)
    {
        return WithLockAsync(subscriptionId, async subscription =>
        {
            if (subscription.Status == SubscriptionStatus.Cancelled)
            {
                return subscription;
            }

            await DetachFromOpenRenewalAsync(subscription, "Subscription cancelled");

            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledDate = DateTime.UtcNow;
            subscription.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            subscription.PausedUntil = null;
            await subscriptionService.SaveChangesAsync([subscription]);

            await notificationService.SendAsync<SubscriptionCancelledEmailNotification>([subscription]);

            return subscription;
        });
    }

    public virtual Task<Subscription> ChangeAsync(string subscriptionId, SubscriptionChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return WithLockAsync(subscriptionId, async subscription =>
        {
            EnsureStatus(subscription, SubscriptionStatus.Active, SubscriptionStatus.Paused, SubscriptionStatus.PastDue, SubscriptionStatus.PendingActivation);

            var detach = false;

            if (change.IntervalUnit != null || change.IntervalCount != null)
            {
                var unit = change.IntervalUnit ?? subscription.IntervalUnit;
                var count = change.IntervalCount ?? subscription.IntervalCount;
                if (count < 1)
                {
                    throw new SubscriptionOperationException("The frequency must be at least 1.");
                }

                var plan = string.IsNullOrEmpty(subscription.PlanId) ? null : await planService.GetNoCloneAsync(subscription.PlanId);
                if (plan != null && plan.Intervals?.Count > 0 && !plan.SupportsInterval(unit, count))
                {
                    throw new SubscriptionOperationException("This frequency is not offered for this product.");
                }

                if (unit != subscription.IntervalUnit || count != subscription.IntervalCount)
                {
                    // Keep the anchor: the next order is one new interval after the last order (or after today).
                    var anchor = subscription.LastOrderDate ?? subscription.StartDate;
                    var next = SubscriptionSchedule.AddInterval(anchor, unit, count, subscription.EffectiveAnchorDay);
                    var earliest = await GetEarliestNextOrderDateAsync(subscription);
                    subscription.NextOrderDate = next < earliest ? earliest : next;
                    subscription.IntervalUnit = unit;
                    subscription.IntervalCount = count;
                    detach = true;
                }
            }

            if (change.Quantity != null)
            {
                if (change.Quantity < 1 || change.Quantity > MaxQuantity)
                {
                    throw new SubscriptionOperationException($"The quantity must be between 1 and {MaxQuantity}.");
                }

                if (change.Quantity != subscription.Quantity)
                {
                    await EnsureQuantityAllowedAsync(subscription.ProductId, change.Quantity.Value);
                    subscription.Quantity = change.Quantity.Value;
                    detach = true;
                }
            }

            if (change.NextOrderDate != null)
            {
                var date = change.NextOrderDate.Value.ToUniversalTime();
                if (date.Date < DateTime.UtcNow.Date.AddDays(1) || date > DateTime.UtcNow.AddYears(1))
                {
                    throw new SubscriptionOperationException("The next delivery date must be between tomorrow and one year from now.");
                }

                subscription.NextOrderDate = date;
                // The customer picked a day: later monthly deliveries keep it.
                subscription.AnchorDay = date.Day;
                detach = true;
            }

            if (!string.IsNullOrEmpty(change.StoredPaymentMethodId) && change.StoredPaymentMethodId != subscription.StoredPaymentMethodId)
            {
                var storedMethod = await storedPaymentMethodService.GetNoCloneAsync(change.StoredPaymentMethodId);
                if (storedMethod == null || storedMethod.CustomerId != subscription.CustomerId || storedMethod.Status != StoredPaymentMethodStatus.Active)
                {
                    throw new SubscriptionOperationException("This payment method can't be used.");
                }

                subscription.StoredPaymentMethodId = storedMethod.Id;
                subscription.PaymentMethodCode = storedMethod.PaymentMethodCode;

                if (subscription.Status == SubscriptionStatus.PastDue)
                {
                    subscription.Status = SubscriptionStatus.Active;
                }
            }

            if (detach)
            {
                await DetachFromOpenRenewalAsync(subscription, "Subscription changed");
            }

            await subscriptionService.SaveChangesAsync([subscription]);

            return subscription;
        });
    }

    /// <summary>
    /// Removes the subscription from an open renewal whose order has not been placed yet.
    /// The scheduler builds a fresh renewal with the new settings when it is due.
    /// </summary>
    protected virtual async Task DetachFromOpenRenewalAsync(Subscription subscription, string reason)
    {
        if (string.IsNullOrEmpty(subscription.CurrentRenewalId))
        {
            return;
        }

        var renewal = await renewalService.GetByIdAsync(subscription.CurrentRenewalId);
        subscription.CurrentRenewalId = null;

        if (renewal == null || !RenewalState.BeforeOrder.Contains(renewal.State))
        {
            return;
        }

        var item = renewal.Items.FirstOrDefault(x => x.SubscriptionId == subscription.Id);
        if (item != null)
        {
            renewal.Items.Remove(item);
        }

        if (renewal.Items.Count == 0)
        {
            renewal.ChangeState(subscription.Status == SubscriptionStatus.Active && reason.StartsWith("Skipped") ? RenewalState.Skipped : RenewalState.Cancelled, reason);
        }
        else
        {
            renewal.AddEvent(RenewalEventType.CustomerAction, $"{subscription.ProductName}: {reason}");
            // Recalculate before the order is placed.
            if (renewal.State == RenewalState.Notified || renewal.State == RenewalState.ReviewRequired)
            {
                renewal.NextProcessingDate = DateTime.UtcNow;
            }
        }

        await renewalService.SaveChangesAsync([renewal]);
    }

    /// <summary>
    /// Runs <paramref name="action"/> on a fresh copy of the subscription, under its customer's lock, which the renewal
    /// processor takes too: a change never races the scheduler or an order being placed (see <see cref="SubscriptionLocks"/>).
    /// </summary>
    protected virtual async Task<Subscription> WithLockAsync(string subscriptionId, Func<Subscription, Task<Subscription>> action)
    {
        var customerId = (await subscriptionService.GetNoCloneAsync(subscriptionId))?.CustomerId
            ?? throw new SubscriptionOperationException($"Subscription {subscriptionId} was not found.");

        return await distributedLock.RunForCustomerAsync(customerId, async () => await action(await GetRequiredAsync(subscriptionId)));
    }

    /// <summary>
    /// The earliest next delivery to set on a resume or a frequency change: late enough for the reminder to go out the
    /// store's lead days before the order.
    /// </summary>
    protected virtual async Task<DateTime> GetEarliestNextOrderDateAsync(Subscription subscription)
    {
        var store = await storeService.GetNoCloneAsync(subscription.StoreId);
        var leadDays = store?.Settings.GetValue<int>(ModuleConstants.Settings.Store.ReminderLeadDays) ?? 0;
        return SubscriptionSchedule.EarliestNextOrderDate(DateTime.UtcNow, leadDays);
    }

    protected virtual async Task<Subscription> GetRequiredAsync(string subscriptionId)
    {
        var subscription = await subscriptionService.GetByIdAsync(subscriptionId);
        return subscription ?? throw new SubscriptionOperationException($"Subscription {subscriptionId} was not found.");
    }

    protected static void EnsureStatus(Subscription subscription, params string[] allowed)
    {
        if (!allowed.Contains(subscription.Status))
        {
            throw new SubscriptionOperationException($"This action isn't available for a subscription that is {subscription.Status}.");
        }
    }

    /// <summary>Checks the product's own ordering rules (pack size, minimum and maximum quantity).</summary>
    protected virtual async Task EnsureQuantityAllowedAsync(string productId, int quantity)
    {
        if (string.IsNullOrEmpty(productId))
        {
            return;
        }

        var product = await itemService.GetNoCloneAsync(productId, ItemResponseGroup.ItemInfo.ToString());
        if (product == null)
        {
            return;
        }

        if (product.PackSize > 1 && quantity % product.PackSize != 0)
        {
            throw new SubscriptionOperationException($"This product is sold in packs of {product.PackSize}. Choose a multiple of {product.PackSize}.");
        }

        if (product.MinQuantity > 0 && quantity < product.MinQuantity)
        {
            throw new SubscriptionOperationException($"The minimum quantity for this product is {product.MinQuantity}.");
        }

        if (product.MaxQuantity > 0 && quantity > product.MaxQuantity)
        {
            throw new SubscriptionOperationException($"The maximum quantity for this product is {product.MaxQuantity}.");
        }
    }

    /// <summary>
    /// The card that paid the order: a card saved with this order, otherwise the customer's saved card for the
    /// payment method that was charged since the order was placed, otherwise their default card.
    /// </summary>
    protected virtual async Task<StoredPaymentMethod> FindStoredPaymentMethodAsync(CustomerOrder order)
    {
        var criteria = AbstractTypeFactory<StoredPaymentMethodSearchCriteria>.TryCreateInstance();
        criteria.InitialOrderId = order.Id;
        criteria.Statuses = [StoredPaymentMethodStatus.Active];
        criteria.Take = 1;
        var savedWithOrder = (await storedPaymentMethodSearchService.SearchAsync(criteria)).Results.FirstOrDefault();
        if (savedWithOrder != null || string.IsNullOrEmpty(order.CustomerId))
        {
            return savedWithOrder;
        }

        var paymentMethodCode = order.InPayments?
            .Where(x => _paidStatuses.Contains(x.PaymentStatus))
            .Select(x => x.GatewayCode)
            .FirstOrDefault(x => !string.IsNullOrEmpty(x));

        var customerCriteria = AbstractTypeFactory<StoredPaymentMethodSearchCriteria>.TryCreateInstance();
        customerCriteria.StoreId = order.StoreId;
        customerCriteria.CustomerId = order.CustomerId;
        customerCriteria.PaymentMethodCode = paymentMethodCode;
        customerCriteria.Statuses = [StoredPaymentMethodStatus.Active];
        customerCriteria.Take = ModuleConstants.SearchPageSize;
        var cards = await storedPaymentMethodSearchService.SearchAllNoCloneAsync(customerCriteria);

        var placedDate = order.CreatedDate == default ? DateTime.MinValue : order.CreatedDate.AddMinutes(-1);

        return cards
                   .Where(x => x.LastUsedDate >= placedDate)
                   .OrderByDescending(x => x.LastUsedDate)
                   .FirstOrDefault()
               ?? cards.FirstOrDefault(x => x.IsDefault);
    }

    protected virtual async Task<string> FindConsentIdAsync(CustomerOrder order)
    {
        // The latest consent of the customer for the cart (the search sorts by acceptance date, newest first).
        var criteria = AbstractTypeFactory<PaymentConsentSearchCriteria>.TryCreateInstance();
        criteria.CustomerId = order.CustomerId;
        criteria.RelatedObjectType = ModuleConstants.ConsentObjectType;
        criteria.RelatedObjectId = order.ShoppingCartId;
        criteria.Purpose = ModuleConstants.ConsentPurpose;
        criteria.Take = 1;
        return (await paymentConsentSearchService.SearchAsync(criteria)).Results.FirstOrDefault()?.Id;
    }

    protected static Address ToCoreAddress(Address source)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<Address>.TryCreateInstance();
        result.AddressType = source.AddressType;
        result.Name = source.Name;
        result.FirstName = source.FirstName;
        result.LastName = source.LastName;
        result.Organization = source.Organization;
        result.Line1 = source.Line1;
        result.Line2 = source.Line2;
        result.City = source.City;
        result.PostalCode = source.PostalCode;
        result.RegionId = source.RegionId;
        result.RegionName = source.RegionName;
        result.CountryCode = source.CountryCode;
        result.CountryName = source.CountryName;
        result.Phone = source.Phone;
        result.Email = source.Email;
        result.OuterId = source.OuterId;
        return result;
    }
}
