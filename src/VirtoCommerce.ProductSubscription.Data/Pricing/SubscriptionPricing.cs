using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.XCart.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Pricing;

/// <summary>
/// The subscription discount for one cart line, carried on the promotion entry's attributes.
/// </summary>
public record SubscriptionLineDiscount(string PlanId, string PlanName, SubscriptionDiscountType DiscountType, decimal DiscountValue);

/// <summary>
/// Ambient discounts for a server-side renewal cart (it has no saved cart id to look choices up by).
/// The renewal engine opens a scope around cart calculation; the promotion middleware reads it.
/// </summary>
public static class SubscriptionDiscountScope
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, SubscriptionLineDiscount>> _current = new();

    public static IReadOnlyDictionary<string, SubscriptionLineDiscount> Current => _current.Value;

    public static IDisposable Begin(IReadOnlyDictionary<string, SubscriptionLineDiscount> discountsByProductId)
    {
        var previous = _current.Value;
        _current.Value = discountsByProductId;
        return new Restore(previous);
    }

    private sealed class Restore(IReadOnlyDictionary<string, SubscriptionLineDiscount> previous) : IDisposable
    {
        public void Dispose()
        {
            _current.Value = previous;
        }
    }
}

public static class SubscriptionPromoAttributes
{
    public const string PlanId = "productSubscription.planId";
    public const string PlanName = "productSubscription.planName";
    public const string DiscountType = "productSubscription.discountType";
    public const string DiscountValue = "productSubscription.discountValue";

    public static void Set(ProductPromoEntry entry, SubscriptionLineDiscount discount)
    {
        entry.Attributes ??= new Dictionary<string, string>();
        entry.Attributes[PlanId] = discount.PlanId;
        entry.Attributes[PlanName] = discount.PlanName;
        entry.Attributes[DiscountType] = discount.DiscountType.ToString();
        entry.Attributes[DiscountValue] = discount.DiscountValue.ToString(CultureInfo.InvariantCulture);
    }

    public static SubscriptionLineDiscount Get(ProductPromoEntry entry)
    {
        if (entry?.Attributes == null || !entry.Attributes.TryGetValue(DiscountType, out var type))
        {
            return null;
        }

        entry.Attributes.TryGetValue(PlanId, out var planId);
        entry.Attributes.TryGetValue(PlanName, out var planName);
        entry.Attributes.TryGetValue(DiscountValue, out var value);

        return new SubscriptionLineDiscount(
            planId,
            planName,
            EnumUtility.SafeParse(type, SubscriptionDiscountType.None),
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m);
    }
}

/// <summary>
/// Added to xCart's promotion pipeline: marks the promotion entries of subscription lines with the plan discount.
/// Checkout carts are resolved from the customer's saved choices (by cart and line id); renewal carts from the ambient scope.
/// </summary>
public class SubscriptionPromoEntriesMiddleware(
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionPlanService planService)
    : IAsyncMiddleware<PromotionEvaluationContextCartMap>
{
    public async Task Run(PromotionEvaluationContextCartMap parameter, Func<PromotionEvaluationContextCartMap, Task> next)
    {
        await next(parameter);

        try
        {
            var context = parameter.PromotionEvaluationContext;
            var cart = parameter.CartAggregate?.Cart;
            if (context == null || cart == null)
            {
                return;
            }

            var discounts = SubscriptionDiscountScope.Current is { } scoped
                ? scoped.ToDictionary(x => x.Key, x => x.Value)
                : await GetCheckoutDiscountsAsync(cart);

            if (discounts.Count == 0)
            {
                return;
            }

            // xCart builds promotion entries per line without an owner, so checkout discounts (keyed by line id)
            // are matched to entries by the line's product.
            var lineProductIds = (cart.Items ?? []).Where(x => !string.IsNullOrEmpty(x.Id)).ToDictionary(x => x.Id, x => x.ProductId);

            foreach (var entry in (context.PromoEntries ?? []).Concat(context.CartPromoEntries ?? []))
            {
                var discount = FindDiscount(entry, discounts, lineProductIds);
                if (discount != null)
                {
                    SubscriptionPromoAttributes.Set(entry, discount);
                }
            }
        }
        catch (Exception)
        {
            // Never break cart calculation because of the subscription discount.
        }
    }

    private static SubscriptionLineDiscount FindDiscount(
        ProductPromoEntry entry,
        Dictionary<string, SubscriptionLineDiscount> discounts,
        Dictionary<string, string> lineProductIds)
    {
        if (entry.Owner is LineItem lineItem && !string.IsNullOrEmpty(lineItem.Id) && discounts.TryGetValue(lineItem.Id, out var byLine))
        {
            return byLine;
        }

        if (string.IsNullOrEmpty(entry.ProductId))
        {
            return null;
        }

        if (discounts.TryGetValue(entry.ProductId, out var byProduct))
        {
            return byProduct;
        }

        return discounts
            .Where(x => lineProductIds.TryGetValue(x.Key, out var productId) && productId == entry.ProductId)
            .Select(x => x.Value)
            .FirstOrDefault();
    }

    /// <summary>Returns discounts keyed by cart line id.</summary>
    protected virtual async Task<Dictionary<string, SubscriptionLineDiscount>> GetCheckoutDiscountsAsync(ShoppingCart cart)
    {
        var result = new Dictionary<string, SubscriptionLineDiscount>();
        if (string.IsNullOrEmpty(cart.Id))
        {
            return result;
        }

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.Take = 1000;
        var choices = (await cartItemSearchService.SearchAsync(criteria)).Results;
        if (choices.Count == 0)
        {
            return result;
        }

        var plans = (await planService.GetAsync(choices.Select(x => x.PlanId).Distinct().ToList())).ToDictionary(x => x.Id);
        var lineIds = cart.Items?.Select(x => x.Id).ToHashSet() ?? [];

        foreach (var choice in choices.Where(x => lineIds.Contains(x.LineItemId)))
        {
            if (plans.TryGetValue(choice.PlanId, out var plan) && plan.IsActive && plan.DiscountType != SubscriptionDiscountType.None)
            {
                result[choice.LineItemId] = new SubscriptionLineDiscount(plan.Id, plan.Name, plan.DiscountType, plan.DiscountValue);
            }
        }

        return result;
    }
}

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

        result ??= new PromotionResult();

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

            result.Rewards.Add(reward);
        }

        return result;
    }
}
