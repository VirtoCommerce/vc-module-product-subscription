using System;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// One subscribed product for one customer. Items due on the same date for the same customer, address and
/// payment method are grouped into one <see cref="Renewal"/> and become one order.
/// </summary>
public class Subscription : AuditableEntity, ICloneable
{
    public string Number { get; set; }
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

    public string PlanId { get; set; }
    public string PlanName { get; set; }
    public SubscriptionIntervalUnit IntervalUnit { get; set; } = SubscriptionIntervalUnit.Month;
    public int IntervalCount { get; set; } = 1;

    /// <summary>Discount agreed at sign-up (copied from the plan).</summary>
    public SubscriptionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    public string Status { get; set; } = SubscriptionStatus.PendingActivation;

    public DateTime StartDate { get; set; }

    /// <summary>Date the next order is placed. The schedule always advances from this date.</summary>
    public DateTime NextOrderDate { get; set; }

    /// <summary>
    /// Day of month monthly and yearly deliveries keep (1–31), so a short month doesn't move every later delivery:
    /// started on the 31st means Jan 31, Feb 28, Mar 31. Null for older data: the day of <see cref="StartDate"/> is used.
    /// </summary>
    public int? AnchorDay { get; set; }

    /// <summary>The anchor day in effect.</summary>
    public int EffectiveAnchorDay => AnchorDay ?? StartDate.Day;

    public DateTime? PausedUntil { get; set; }
    public int SkipCount { get; set; }

    public DateTime? CancelledDate { get; set; }
    public string CancelReason { get; set; }

    public string Currency { get; set; }
    public string LanguageCode { get; set; }

    public Address ShippingAddress { get; set; }
    public string ShippingMethodCode { get; set; }
    public string ShippingMethodOption { get; set; }

    public string PaymentMethodCode { get; set; }
    public string StoredPaymentMethodId { get; set; }
    public string ConsentId { get; set; }

    public string InitialOrderId { get; set; }
    public string InitialOrderNumber { get; set; }
    public string LastOrderId { get; set; }
    public string LastOrderNumber { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public int OrdersCount { get; set; }

    /// <summary>Unit price paid at sign-up, after the subscription discount.</summary>
    public decimal? ReferencePrice { get; set; }

    public DateTime? LastAnnualReminderDate { get; set; }

    /// <summary>Renewal currently open for this subscription, if any.</summary>
    public string CurrentRenewalId { get; set; }

    public string OuterId { get; set; }

    public virtual bool IsActiveForRenewal => Status == SubscriptionStatus.Active;

    public virtual object Clone()
    {
        var result = (Subscription)MemberwiseClone();
        result.ShippingAddress = (Address)ShippingAddress?.Clone();
        return result;
    }
}

public static class SubscriptionStatus
{
    /// <summary>Created from an order whose payment is not authorized yet.</summary>
    public const string PendingActivation = "PendingActivation";
    public const string Active = "Active";
    public const string Paused = "Paused";
    /// <summary>A renewal could not be paid; renewals stop until the payment method is fixed.</summary>
    public const string PastDue = "PastDue";
    public const string Cancelled = "Cancelled";

    public static string[] All { get; } = [PendingActivation, Active, Paused, PastDue, Cancelled];
}
