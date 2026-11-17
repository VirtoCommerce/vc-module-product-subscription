using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

/// <summary>
/// The subscription marks of a page of the signed-in customer's orders, in one request. The order types carry the same
/// data as fields (<c>subscriptionInfo</c>, <c>subscription</c>); this query is for a client whose generated types
/// only know this module's schema.
/// </summary>
public class GetOrderSubscriptionInfosQuery : Query<IList<OrderSubscriptionInfo>>, IHasUserId
{
    public IList<string> OrderIds { get; set; }
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(nameof(OrderIds), description: "The orders to mark (at most 100)");
    }

    public override void Map(IResolveFieldContext context)
    {
        OrderIds = context.GetArgument<List<string>>(nameof(OrderIds));
    }
}

public class GetOrderSubscriptionInfosQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetOrderSubscriptionInfosQuery, IList<OrderSubscriptionInfo>, NonNullGraphType<ListGraphType<NonNullGraphType<OrderSubscriptionInfoType>>>>(authorizationService)
{
    protected override string Name => "orderSubscriptionInfos";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetOrderSubscriptionInfosQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

public class GetOrderSubscriptionInfosQueryHandler(IOrderSubscriptionInfoService orderSubscriptionInfoService)
    : IQueryHandler<GetOrderSubscriptionInfosQuery, IList<OrderSubscriptionInfo>>
{
    /// <summary>Only the customer's own subscriptions and renewals: an order id of someone else gives nothing away.</summary>
    public virtual Task<IList<OrderSubscriptionInfo>> Handle(GetOrderSubscriptionInfosQuery request, CancellationToken cancellationToken)
        => orderSubscriptionInfoService.GetForOrdersAsync(request.OrderIds, request.UserId);
}
