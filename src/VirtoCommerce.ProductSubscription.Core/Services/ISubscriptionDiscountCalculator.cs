using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

/// <summary>
/// The subscription discount for one item, computed the way the cart applies it, so a price shown before checkout
/// is the price the cart charges.
/// </summary>
public interface ISubscriptionDiscountCalculator
{
    /// <summary>The discount per item off <paramref name="price"/> (the item price after its other discounts).</summary>
    decimal GetDiscountPerItem(SubscriptionDiscountType discountType, decimal discountValue, decimal price, Currency currency);
}
