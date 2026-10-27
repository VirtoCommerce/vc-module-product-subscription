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
}
