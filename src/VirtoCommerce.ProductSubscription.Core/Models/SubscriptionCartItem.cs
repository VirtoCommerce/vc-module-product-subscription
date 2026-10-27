using System;
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
