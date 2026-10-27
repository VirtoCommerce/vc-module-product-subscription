using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionOfferService(
    ISubscriptionPlanSearchService planSearchService,
    IItemService itemService,
    IStoreService storeService)
    : ISubscriptionOfferService
{
    private const int MaxPlans = 500;

    public virtual async Task<IDictionary<string, SubscriptionOffer>> GetOffersAsync(string storeId, IList<string> productIds)
    {
        var result = new Dictionary<string, SubscriptionOffer>();
        if (string.IsNullOrEmpty(storeId) || productIds.IsNullOrEmpty())
        {
            return result;
        }

        var store = await storeService.GetNoCloneAsync(storeId);
        if (store == null || !store.Settings.GetValue<bool>(ModuleConstants.Settings.Store.Enabled))
        {
            return result;
        }

        var criteria = AbstractTypeFactory<SubscriptionPlanSearchCriteria>.TryCreateInstance();
        criteria.StoreId = storeId;
        criteria.IsActive = true;
        criteria.Take = MaxPlans;
        var plans = (await planSearchService.SearchAsync(criteria)).Results
            .Where(x => x.Intervals?.Count > 0)
            .OrderByDescending(x => x.Priority)
            .ToList();

        if (plans.Count == 0)
        {
            return result;
        }

        var distinctIds = productIds.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var needCategories = plans.Any(x => x.CategoryIds?.Count > 0);
        var products = needCategories || plans.Any(x => x.ProductIds?.Count > 0)
            ? (await itemService.GetNoCloneAsync(distinctIds, (ItemResponseGroup.ItemInfo | ItemResponseGroup.Outlines).ToString())).ToDictionary(x => x.Id)
            : new Dictionary<string, CatalogProduct>();

        foreach (var productId in distinctIds)
        {
            products.TryGetValue(productId, out var product);
            var plan = plans.FirstOrDefault(x => Matches(x, productId, product));
            if (plan != null)
            {
                result[productId] = ToOffer(plan, productId);
            }
        }

        return result;
    }

    protected virtual bool Matches(SubscriptionPlan plan, string productId, CatalogProduct product)
    {
        if (plan.ProductIds?.Contains(productId) == true)
        {
            return true;
        }

        if (product != null && !string.IsNullOrEmpty(product.MainProductId) && plan.ProductIds?.Contains(product.MainProductId) == true)
        {
            return true;
        }

        if (plan.CategoryIds.IsNullOrEmpty() || product == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(product.CategoryId) && plan.CategoryIds.Contains(product.CategoryId))
        {
            return true;
        }

        var outlineCategoryIds = product.Outlines?
            .SelectMany(x => x.Items ?? [])
            .Select(x => x.Id)
            .ToHashSet() ?? [];

        return plan.CategoryIds.Any(outlineCategoryIds.Contains);
    }

    protected virtual SubscriptionOffer ToOffer(SubscriptionPlan plan, string productId)
    {
        return new SubscriptionOffer
        {
            ProductId = productId,
            PlanId = plan.Id,
            PlanName = plan.Name,
            Description = plan.Description,
            DiscountType = plan.DiscountType,
            DiscountValue = plan.DiscountValue,
            Intervals = plan.Intervals.Select(x => (SubscriptionPlanInterval)x.Clone()).ToList(),
        };
    }
}
