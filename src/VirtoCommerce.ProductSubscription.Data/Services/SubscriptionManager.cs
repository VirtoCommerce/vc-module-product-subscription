using System;
using System.Collections.Generic;
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
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.ProductSubscription.Core.Services;
using Address = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.ProductSubscription.Data.Services;

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
    ILogger<SubscriptionManager> logger)
    : ISubscriptionManager
{
    public const string CartObjectType = "ShoppingCart";

    private static readonly PaymentStatus[] _paidStatuses = [PaymentStatus.Authorized, PaymentStatus.Paid];

    public virtual async Task<IList<Subscription>> CreateFromOrderAsync(CustomerOrder order)
    {
        if (order == null || order.IsPrototype || string.IsNullOrEmpty(order.ShoppingCartId) || order.Items.IsNullOrEmpty())
        {
            return [];
        }

        var choicesCriteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        choicesCriteria.CartIds = [order.ShoppingCartId];
        choicesCriteria.Take = 1000;
        var choices = (await cartItemSearchService.SearchAsync(choicesCriteria)).Results;
        if (choices.Count == 0)
        {
            return [];
        }

        // Idempotent: never create subscriptions twice for the same order.
        var existingCriteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        existingCriteria.InitialOrderId = order.Id;
        existingCriteria.Take = 1;
        if ((await subscriptionSearchService.SearchAsync(existingCriteria)).TotalCount > 0)
        {
            return [];
        }

        var plans = (await planService.GetAsync(choices.Select(x => x.PlanId).Distinct().ToList())).ToDictionary(x => x.Id);
        var storedPaymentMethod = await FindStoredPaymentMethodAsync(order);
        var consentId = await FindConsentIdAsync(order);
        var paymentAuthorized = order.InPayments?.Any(x => _paidStatuses.Contains(x.PaymentStatus)) == true;

        var shipment = order.Shipments?.FirstOrDefault();
        var payment = order.InPayments?.FirstOrDefault();
        var shippingAddress = ToCoreAddress(shipment?.DeliveryAddress)
            ?? ToCoreAddress(order.Addresses?.FirstOrDefault(x => x.AddressType == AddressType.Shipping || x.AddressType == AddressType.BillingAndShipping))
            ?? ToCoreAddress(order.Addresses?.FirstOrDefault());

        var now = DateTime.UtcNow;
        var subscriptions = new List<Subscription>();
        var usedChoices = new List<SubscriptionCartItem>();

        foreach (var choice in choices)
        {
            var lineItem = order.Items.FirstOrDefault(x => x.ProductId == choice.ProductId);
            if (lineItem == null || !plans.TryGetValue(choice.PlanId, out var plan))
            {
                continue;
            }

            var subscription = AbstractTypeFactory<Subscription>.TryCreateInstance();
            subscription.StoreId = order.StoreId;
            subscription.CustomerId = order.CustomerId;
            subscription.CustomerName = order.CustomerName;
            subscription.CustomerEmail = shippingAddress?.Email ?? order.Addresses?.Select(x => x.Email).FirstOrDefault(x => !string.IsNullOrEmpty(x));
            subscription.OrganizationId = order.OrganizationId;
            subscription.ProductId = lineItem.ProductId;
            subscription.Sku = lineItem.Sku;
            subscription.ProductName = lineItem.Name;
            subscription.ImageUrl = lineItem.ImageUrl;
            subscription.Quantity = Math.Max(1, lineItem.Quantity);
            subscription.PlanId = plan.Id;
            subscription.PlanName = plan.Name;
            subscription.IntervalUnit = choice.IntervalUnit;
            subscription.IntervalCount = Math.Max(1, choice.IntervalCount);
            subscription.DiscountType = plan.DiscountType;
            subscription.DiscountValue = plan.DiscountValue;
            subscription.Status = paymentAuthorized ? SubscriptionStatus.Active : SubscriptionStatus.PendingActivation;
            subscription.StartDate = now;
            subscription.NextOrderDate = SubscriptionSchedule.AddInterval(order.CreatedDate == default ? now : order.CreatedDate, subscription.IntervalUnit, subscription.IntervalCount);
            subscription.Currency = order.Currency;
            subscription.LanguageCode = order.LanguageCode;
            subscription.ShippingAddress = shippingAddress;
            subscription.ShippingMethodCode = shipment?.ShipmentMethodCode;
            subscription.ShippingMethodOption = shipment?.ShipmentMethodOption;
            subscription.PaymentMethodCode = storedPaymentMethod?.PaymentMethodCode ?? payment?.GatewayCode;
            subscription.StoredPaymentMethodId = storedPaymentMethod?.Id;
            subscription.ConsentId = consentId;
            subscription.InitialOrderId = order.Id;
            subscription.InitialOrderNumber = order.Number;
            subscription.LastOrderId = order.Id;
            subscription.LastOrderNumber = order.Number;
            subscription.LastOrderDate = now;
            subscription.OrdersCount = 1;
            subscription.ReferencePrice = lineItem.PlacedPrice;
            subscription.LastAnnualReminderDate = now;

            subscriptions.Add(subscription);
            usedChoices.Add(choice);
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

    public virtual async Task<IList<Subscription>> ActivateForOrderAsync(CustomerOrder order)
    {
        if (order?.InPayments?.Any(x => _paidStatuses.Contains(x.PaymentStatus)) != true)
        {
            return [];
        }

        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.InitialOrderId = order.Id;
        criteria.Statuses = [SubscriptionStatus.PendingActivation];
        criteria.Take = 1000;
        var pending = (await subscriptionSearchService.SearchAsync(criteria)).Results;
        if (pending.Count == 0)
        {
            return [];
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
    }

    public virtual async Task<Subscription> SkipNextAsync(string subscriptionId)
    {
        var subscription = await GetRequiredAsync(subscriptionId);
        EnsureStatus(subscription, SubscriptionStatus.Active, SubscriptionStatus.PastDue);

        await DetachFromOpenRenewalAsync(subscription, "Skipped by customer");

        subscription.NextOrderDate = SubscriptionSchedule.AddInterval(subscription.NextOrderDate, subscription.IntervalUnit, subscription.IntervalCount);
        subscription.SkipCount++;
        await subscriptionService.SaveChangesAsync([subscription]);

        return subscription;
    }

    public virtual async Task<Subscription> PauseAsync(string subscriptionId, DateTime? resumeDate)
    {
        var subscription = await GetRequiredAsync(subscriptionId);
        EnsureStatus(subscription, SubscriptionStatus.Active, SubscriptionStatus.PastDue);

        if (resumeDate != null && resumeDate.Value.ToUniversalTime() <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("The resume date must be in the future.");
        }

        await DetachFromOpenRenewalAsync(subscription, "Subscription paused");

        subscription.Status = SubscriptionStatus.Paused;
        subscription.PausedUntil = resumeDate?.ToUniversalTime();
        await subscriptionService.SaveChangesAsync([subscription]);

        return subscription;
    }

    public virtual async Task<Subscription> ResumeAsync(string subscriptionId)
    {
        var subscription = await GetRequiredAsync(subscriptionId);
        EnsureStatus(subscription, SubscriptionStatus.Paused);

        subscription.Status = SubscriptionStatus.Active;
        subscription.PausedUntil = null;

        var earliest = DateTime.UtcNow.AddDays(1);
        if (subscription.NextOrderDate < earliest)
        {
            subscription.NextOrderDate = earliest;
        }

        await subscriptionService.SaveChangesAsync([subscription]);

        return subscription;
    }

    public virtual async Task<Subscription> CancelAsync(string subscriptionId, string reason)
    {
        var subscription = await GetRequiredAsync(subscriptionId);
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
    }

    public virtual async Task<Subscription> ChangeAsync(string subscriptionId, SubscriptionChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var subscription = await GetRequiredAsync(subscriptionId);
        EnsureStatus(subscription, SubscriptionStatus.Active, SubscriptionStatus.Paused, SubscriptionStatus.PastDue, SubscriptionStatus.PendingActivation);

        var detach = false;

        if (change.IntervalUnit != null || change.IntervalCount != null)
        {
            var unit = change.IntervalUnit ?? subscription.IntervalUnit;
            var count = change.IntervalCount ?? subscription.IntervalCount;
            if (count < 1)
            {
                throw new InvalidOperationException("The frequency must be at least 1.");
            }

            var plan = string.IsNullOrEmpty(subscription.PlanId) ? null : await planService.GetNoCloneAsync(subscription.PlanId);
            if (plan != null && plan.Intervals?.Count > 0 && !plan.SupportsInterval(unit, count))
            {
                throw new InvalidOperationException("This frequency is not offered for this product.");
            }

            if (unit != subscription.IntervalUnit || count != subscription.IntervalCount)
            {
                // Keep the anchor: the next order is one new interval after the last order (or after today).
                var anchor = subscription.LastOrderDate ?? subscription.StartDate;
                var next = SubscriptionSchedule.AddInterval(anchor, unit, count);
                var earliest = DateTime.UtcNow.AddDays(1);
                subscription.NextOrderDate = next < earliest ? earliest : next;
                subscription.IntervalUnit = unit;
                subscription.IntervalCount = count;
                detach = true;
            }
        }

        if (change.Quantity != null)
        {
            if (change.Quantity < 1 || change.Quantity > 9999)
            {
                throw new InvalidOperationException("The quantity must be between 1 and 9999.");
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
                throw new InvalidOperationException("The next delivery date must be between tomorrow and one year from now.");
            }

            subscription.NextOrderDate = date;
            detach = true;
        }

        if (!string.IsNullOrEmpty(change.StoredPaymentMethodId) && change.StoredPaymentMethodId != subscription.StoredPaymentMethodId)
        {
            var storedMethod = await storedPaymentMethodService.GetNoCloneAsync(change.StoredPaymentMethodId);
            if (storedMethod == null || storedMethod.CustomerId != subscription.CustomerId || storedMethod.Status != StoredPaymentMethodStatus.Active)
            {
                throw new InvalidOperationException("This payment method can't be used.");
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

    protected virtual async Task<Subscription> GetRequiredAsync(string subscriptionId)
    {
        var subscription = await subscriptionService.GetByIdAsync(subscriptionId);
        return subscription ?? throw new InvalidOperationException($"Subscription {subscriptionId} was not found.");
    }

    protected static void EnsureStatus(Subscription subscription, params string[] allowed)
    {
        if (!allowed.Contains(subscription.Status))
        {
            throw new InvalidOperationException($"This action isn't available for a subscription that is {subscription.Status}.");
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
            throw new InvalidOperationException($"This product is sold in packs of {product.PackSize}. Choose a multiple of {product.PackSize}.");
        }

        if (product.MinQuantity > 0 && quantity < product.MinQuantity)
        {
            throw new InvalidOperationException($"The minimum quantity for this product is {product.MinQuantity}.");
        }

        if (product.MaxQuantity > 0 && quantity > product.MaxQuantity)
        {
            throw new InvalidOperationException($"The maximum quantity for this product is {product.MaxQuantity}.");
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
        customerCriteria.Take = 100;
        var cards = (await storedPaymentMethodSearchService.SearchAsync(customerCriteria)).Results;

        var placedDate = order.CreatedDate == default ? DateTime.MinValue : order.CreatedDate.AddMinutes(-1);

        return cards
                   .Where(x => x.LastUsedDate >= placedDate)
                   .OrderByDescending(x => x.LastUsedDate)
                   .FirstOrDefault()
               ?? cards.FirstOrDefault(x => x.IsDefault);
    }

    protected virtual async Task<string> FindConsentIdAsync(CustomerOrder order)
    {
        var criteria = AbstractTypeFactory<PaymentConsentSearchCriteria>.TryCreateInstance();
        criteria.RelatedObjectType = CartObjectType;
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
