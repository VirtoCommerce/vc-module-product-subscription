using System.Collections.Generic;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Handlers;

/// <summary>The order whose change <see cref="OrderChangedJob"/> processes.</summary>
public class OrderChangedJobPayload
{
    public string OrderId { get; set; }

    /// <summary>True when the job was enqueued for the order's creation.</summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// The cart's subscription choices, copied when creating the subscriptions during checkout failed: the cart change
    /// right after checkout deletes them, so the job creates the subscriptions from this copy.
    /// </summary>
    public IList<SubscriptionCartItem> Choices { get; set; }

    /// <summary>The consent given for the order's checkout, taken with <see cref="Choices"/>: xOrder replaces the cart's
    /// CheckoutId after the order, so the job could no longer find it.</summary>
    public string ConsentId { get; set; }
}
