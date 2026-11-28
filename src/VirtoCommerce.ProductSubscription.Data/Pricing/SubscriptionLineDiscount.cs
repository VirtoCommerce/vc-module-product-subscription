using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Pricing;

/// <summary>
/// The subscription discount for one cart line, carried on the promotion entry's attributes.
/// </summary>
public record SubscriptionLineDiscount(string PlanId, string PlanName, SubscriptionDiscountType DiscountType, decimal DiscountValue);
