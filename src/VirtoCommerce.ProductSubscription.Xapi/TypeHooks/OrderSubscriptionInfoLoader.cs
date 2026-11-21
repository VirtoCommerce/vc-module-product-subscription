using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Xapi.TypeHooks;

/// <summary>
/// Loads the subscription info of every order in a GraphQL response with one call: an order list asks for all its
/// orders at once, and a line item shares its order's entry.
/// </summary>
internal static class OrderSubscriptionInfoLoader
{
    public static IDataLoaderResult<OrderSubscriptionInfo> Load(IResolveFieldContext context, string orderId)
    {
        var accessor = context.RequestServices.GetRequiredService<IDataLoaderContextAccessor>();
        var service = context.RequestServices.GetRequiredService<IOrderSubscriptionInfoService>();

        var loader = accessor.Context.GetOrAddBatchLoader<string, OrderSubscriptionInfo>("product_subscription_order_infos", async orderIds =>
            (await service.GetForOrdersAsync(orderIds.ToList())).ToDictionary(x => x.OrderId));

        return loader.LoadAsync(orderId);
    }
}
