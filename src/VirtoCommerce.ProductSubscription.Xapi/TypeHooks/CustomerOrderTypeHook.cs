using System.Linq;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Schemas;
using OrderLineItem = VirtoCommerce.OrdersModule.Core.Model.LineItem;

namespace VirtoCommerce.ProductSubscription.Xapi.TypeHooks;

/// <summary>
/// Adds <c>subscriptionInfo</c> to xOrder's order type without changing xOrder: whether a renewal placed the order and
/// which subscriptions its lines belong to. The order is already authorized by xOrder, so the info isn't filtered by
/// customer (an organization buyer sees a colleague's order with its marks).
/// </summary>
public class CustomerOrderTypeHook : IGraphTypeHook
{
    public string TypeName { get; set; } = nameof(CustomerOrderType);

    public void BeforeTypeInitialized(IGraphType graphType)
    {
        if (graphType is not CustomerOrderType orderType)
        {
            return;
        }

        orderType.Field<OrderSubscriptionInfoType>("subscriptionInfo")
            .Description("The subscription side of this order: the renewal that placed it and the subscriptions behind its lines; null when it has none")
            .Resolve(context =>
            {
                var orderId = ((CustomerOrderAggregate)context.Source).Order?.Id;
                return string.IsNullOrEmpty(orderId) ? null : OrderSubscriptionInfoLoader.Load(context, orderId);
            });
    }
}

/// <summary>Adds <c>subscription</c> to xOrder's order line type: the subscription that ordered (or started with) this product.</summary>
public class OrderLineItemTypeHook : IGraphTypeHook
{
    public string TypeName { get; set; } = nameof(OrderLineItemType);

    public void BeforeTypeInitialized(IGraphType graphType)
    {
        if (graphType is not OrderLineItemType lineItemType)
        {
            return;
        }

        lineItemType.Field<OrderSubscriptionLineType>("subscription")
            .Description("The subscription behind this line, if any")
            .Resolve(context =>
            {
                var orderId = context.GetValueForSource<CustomerOrderAggregate>()?.Order?.Id;
                var productId = ((OrderLineItem)context.Source).ProductId;
                if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(productId))
                {
                    return null;
                }

                return OrderSubscriptionInfoLoader.Load(context, orderId)
                    .Then(info => info?.Subscriptions.FirstOrDefault(x => x.ProductId == productId));
            });
    }
}
