using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PipelineNet.Middleware;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.XCart.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Pricing;

/// <summary>
/// Added to xCart's promotion pipeline: marks the promotion entries of subscription lines with the plan discount.
/// Checkout carts are resolved from the customer's saved choices (by cart and line id); renewal carts from the ambient scope.
/// </summary>
public class SubscriptionPromoEntriesMiddleware(
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionPlanService planService,
    ILogger<SubscriptionPromoEntriesMiddleware> logger)
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
        catch (Exception ex)
        {
            // Never break cart calculation because of the subscription discount, but say why it is missing.
            logger.LogError(ex, "Failed to apply the subscription discount to cart {CartId}", parameter.CartAggregate?.Cart?.Id);
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
