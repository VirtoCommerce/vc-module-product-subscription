using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// The customer's choice to subscribe to a cart line, stored next to the cart so the Cart module stays untouched.
/// When the order is placed, each choice becomes a <see cref="Subscription"/>.
/// </summary>
public class SubscriptionCartItem : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }
    public string CartId { get; set; }
    public string LineItemId { get; set; }
    public string ProductId { get; set; }
    public string CustomerId { get; set; }

    public string PlanId { get; set; }
    public SubscriptionIntervalUnit IntervalUnit { get; set; }
    public int IntervalCount { get; set; } = 1;

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}

/// <summary>
/// The subscription offer shown for a product: the matching plan, its frequencies and discount.
/// </summary>
public class SubscriptionOffer
{
    public string ProductId { get; set; }
    public string PlanId { get; set; }
    public string PlanName { get; set; }
    public string Description { get; set; }
    public SubscriptionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public IList<SubscriptionPlanInterval> Intervals { get; set; } = new List<SubscriptionPlanInterval>();
}

/// <summary>
/// Subscription terms the customer agrees to at checkout (US auto-renewal disclosure).
/// </summary>
public class SubscriptionTerms
{
    public string Version { get; set; }
    public string Text { get; set; }
    public string Hash { get; set; }
}
