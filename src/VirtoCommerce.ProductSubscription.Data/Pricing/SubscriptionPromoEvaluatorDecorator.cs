using System;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Pricing;

/// <summary>
/// Decorates the Marketing evaluator: after regular promotions are evaluated, adds the subscription discount
/// as a line-item reward for every promotion entry marked by <see cref="SubscriptionPromoEntriesMiddleware"/>.
/// xCart applies it like any other discount, so it shows in the cart and is carried into the order.
/// </summary>
public class SubscriptionPromoEvaluatorDecorator(IMarketingPromoEvaluator inner) : IMarketingPromoEvaluator
{
    public const string PromotionIdPrefix = "product-subscription-";

    public virtual async Task<PromotionResult> EvaluatePromotionAsync(IEvaluationContext context)
    {
        var result = await inner.EvaluatePromotionAsync(context);

        if (context is not PromotionEvaluationContext promoContext)
        {
            return result;
        }

        var entries = (promoContext.PromoEntries ?? []).Concat(promoContext.CartPromoEntries ?? [])
            .Select(x => (Entry: x, Discount: SubscriptionPromoAttributes.Get(x)))
            .Where(x => x.Discount != null && x.Discount.DiscountType != SubscriptionDiscountType.None && x.Discount.DiscountValue > 0)
            .GroupBy(x => x.Entry.ProductId)
            .Select(x => x.First())
            .ToList();

        if (entries.Count == 0)
        {
            return result;
        }

        // The inner policy caches its result, so never add to it: copy into a fresh result instead.
        var combined = new PromotionResult();
        foreach (var reward in result?.Rewards ?? [])
        {
            // Drop a subscription reward that may already be there (e.g. from a cached copy).
            if (reward.PromotionId?.StartsWith(PromotionIdPrefix, StringComparison.Ordinal) != true)
            {
                combined.Rewards.Add(reward);
            }
        }

        foreach (var (entry, discount) in entries)
        {
            var promotion = AbstractTypeFactory<Promotion>.TryCreateInstance();
            promotion.Id = PromotionIdPrefix + discount.PlanId;
            promotion.Name = string.IsNullOrEmpty(discount.PlanName) ? "Subscribe & Save" : discount.PlanName;
            promotion.Description = "Subscription discount";
            promotion.IsActive = true;

            var reward = new CatalogItemAmountReward
            {
                IsValid = true,
                ProductId = entry.ProductId,
                Promotion = promotion,
                PromotionId = promotion.Id,
                AmountType = discount.DiscountType == SubscriptionDiscountType.Percent ? RewardAmountType.Relative : RewardAmountType.Absolute,
                Amount = discount.DiscountValue,
                Quantity = 0,
            };

            combined.Rewards.Add(reward);
        }

        return combined;
    }
}
