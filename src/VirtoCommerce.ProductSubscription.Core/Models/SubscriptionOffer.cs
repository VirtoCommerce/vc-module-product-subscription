using System.Collections.Generic;

namespace VirtoCommerce.ProductSubscription.Core.Models;

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

    /// <summary>Code of the currency of <see cref="Price"/>, <see cref="DiscountAmount"/> and <see cref="SubscriptionPrice"/>.</summary>
    public string Currency { get; set; }

    /// <summary>The product's price for the customer, after its regular discounts; null when the product has no price.</summary>
    public decimal? Price { get; set; }

    /// <summary>The subscription discount per item, computed the way the cart applies it.</summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>The price per item with the subscription discount: what the cart charges for a subscribed item.</summary>
    public decimal? SubscriptionPrice { get; set; }
}
