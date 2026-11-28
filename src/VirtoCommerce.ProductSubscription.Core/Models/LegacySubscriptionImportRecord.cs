using System;
using VirtoCommerce.CoreModule.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// One subscribed product taken from the old Virto subscription module (VirtoCommerce.Subscription), in a shape that
/// does not depend on that module. An old subscription whose order prototype has several products becomes several
/// records, because subscriptions in this module are item-level.
/// </summary>
public class LegacySubscriptionImportRecord : ICloneable
{
    /// <summary>Id of the subscription in the old module.</summary>
    public string LegacySubscriptionId { get; set; }

    /// <summary>Number of the subscription in the old module. Reported back only: imported subscriptions get new numbers.</summary>
    public string LegacySubscriptionNumber { get; set; }

    public string StoreId { get; set; }

    public string CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
    public string OrganizationId { get; set; }

    public string ProductId { get; set; }
    public string Sku { get; set; }
    public string ProductName { get; set; }
    public string ImageUrl { get; set; }
    public int Quantity { get; set; } = 1;

    public SubscriptionIntervalUnit IntervalUnit { get; set; } = SubscriptionIntervalUnit.Month;
    public int IntervalCount { get; set; } = 1;

    public LegacySubscriptionStatus Status { get; set; } = LegacySubscriptionStatus.Active;

    /// <summary>
    /// Date the next order is due. In the old module this is the end of the current period (CurrentPeriodEnd):
    /// the next recurring order is created when it passes. A date in the past is moved to tomorrow on import.
    /// </summary>
    public DateTime? NextOrderDate { get; set; }

    public DateTime? StartDate { get; set; }

    /// <summary>Start of the period that was ordered last (old CurrentPeriodStart).</summary>
    public DateTime? LastOrderDate { get; set; }

    public DateTime? CancelledDate { get; set; }
    public string CancelReason { get; set; }

    public Address ShippingAddress { get; set; }
    public string ShippingMethodCode { get; set; }
    public string ShippingMethodOption { get; set; }

    /// <summary>Payment method code (gateway code) of the old order prototype. No saved card exists for legacy subscriptions.</summary>
    public string PaymentMethodCode { get; set; }

    public string Currency { get; set; }
    public string LanguageCode { get; set; }

    /// <summary>
    /// Subscription discount to keep. When empty and a plan is matched, the plan's discount is used; otherwise none.
    /// </summary>
    public SubscriptionDiscountType? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }

    /// <summary>Unit price the customer paid in the old module (old prototype line item placed price).</summary>
    public decimal? ReferencePrice { get; set; }

    /// <summary>
    /// Key that makes the import idempotent; stored as the subscription's OuterId. Defaults to
    /// <c>legacy:{LegacySubscriptionId}</c>; the JSON mapper adds <c>:{ProductId}</c> when an old subscription has several products.
    /// </summary>
    public string OuterId { get; set; }

    public virtual object Clone()
    {
        var result = (LegacySubscriptionImportRecord)MemberwiseClone();
        result.ShippingAddress = (Address)ShippingAddress?.Clone();
        return result;
    }
}
