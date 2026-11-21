using System;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Pricing;

/// <summary>
/// Computes the subscription discount with the reward <see cref="SubscriptionPromoEvaluatorDecorator"/> adds to the cart,
/// the way xCart applies a line-item reward: per item, off the price left after the line's other discounts.
/// </summary>
public class SubscriptionDiscountCalculator : ISubscriptionDiscountCalculator
{
    public virtual decimal GetDiscountPerItem(SubscriptionDiscountType discountType, decimal discountValue, decimal price, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (discountType == SubscriptionDiscountType.None || discountValue <= 0 || price <= 0)
        {
            return 0m;
        }

        var reward = SubscriptionPromoEvaluatorDecorator.CreateReward(productId: null, new SubscriptionLineDiscount(null, null, discountType, discountValue));
        var discount = reward.GetAmountPerItem(price, 1, currency);

        return Math.Clamp(discount, 0m, price);
    }
}
