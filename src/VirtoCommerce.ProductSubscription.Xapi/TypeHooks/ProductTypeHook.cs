using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.TypeHooks;

/// <summary>Adds <c>subscriptionOffer</c> to xCatalog's Product type without changing xCatalog.</summary>
public class ProductTypeHook : IGraphTypeHook
{
    public string TypeName { get; set; } = "Product";

    public void BeforeTypeInitialized(IGraphType graphType)
    {
        if (graphType is not ProductType productType)
        {
            return;
        }

        var field = FieldCreator.CreateFieldAsync<ExpProduct, SubscriptionOfferType>(
            "subscriptionOffer",
            "Subscribe & Save offer for this product, if any",
            resolve: async fieldContext =>
            {
                if (fieldContext.Source == null)
                {
                    return null;
                }

                var storeId = fieldContext.GetArgumentOrValue<string>("storeId");
                var dataLoader = fieldContext.RequestServices.GetRequiredService<IDataLoaderContextAccessor>();
                var loader = dataLoader.Context.GetOrAddBatchLoader<string, SubscriptionOffer>($"product_subscription_offer_{storeId}", async productIds =>
                {
                    var offerService = fieldContext.RequestServices.GetRequiredService<ISubscriptionOfferService>();
                    var offers = await offerService.GetOffersAsync(storeId, productIds.ToList());
                    return new Dictionary<string, SubscriptionOffer>(offers);
                });

                await System.Threading.Tasks.Task.CompletedTask;
                return loader.LoadAsync(fieldContext.Source.Id);
            });

        productType.AddField(field);
    }
}
