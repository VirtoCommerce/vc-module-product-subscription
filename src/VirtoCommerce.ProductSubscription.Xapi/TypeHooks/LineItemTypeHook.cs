using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.TypeHooks;

/// <summary>Adds <c>subscription</c> (the customer's subscription choice) to xCart's LineItem type without changing xCart.</summary>
public class LineItemTypeHook : IGraphTypeHook
{
    public string TypeName { get; set; } = "LineItemType";

    public void BeforeTypeInitialized(IGraphType graphType)
    {
        if (graphType is not LineItemType lineItemType)
        {
            return;
        }

        var field = FieldCreator.CreateFieldAsync<LineItem, SubscriptionCartItemType>(
            "subscription",
            "Subscription chosen for this cart line, if any",
            resolve: async fieldContext =>
            {
                var cartAggregate = fieldContext.GetValueForSource<CartAggregate>();
                var cartId = cartAggregate?.Cart?.Id;
                if (fieldContext.Source == null || string.IsNullOrEmpty(cartId))
                {
                    return null;
                }

                var dataLoader = fieldContext.RequestServices.GetRequiredService<IDataLoaderContextAccessor>();
                var loader = dataLoader.Context.GetOrAddBatchLoader<string, SubscriptionCartItem>($"cart_subscription_items_{cartId}", async lineItemIds =>
                {
                    var searchService = fieldContext.RequestServices.GetRequiredService<ISubscriptionCartItemSearchService>();
                    var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
                    criteria.CartIds = [cartId];
                    criteria.Take = 1000;
                    var items = (await searchService.SearchAsync(criteria)).Results;
                    var wanted = lineItemIds.ToHashSet();
                    return items.Where(x => wanted.Contains(x.LineItemId)).GroupBy(x => x.LineItemId).ToDictionary(x => x.Key, x => x.First());
                });

                await System.Threading.Tasks.Task.CompletedTask;
                return loader.LoadAsync(fieldContext.Source.Id);
            });

        lineItemType.AddField(field);
    }
}
