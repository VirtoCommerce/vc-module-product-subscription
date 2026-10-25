using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetCartSubscriptionItemsQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetCartSubscriptionItemsQuery, IList<SubscriptionCartItem>, NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionCartItemType>>>>(authorizationService)
{
    protected override string Name => "cartSubscriptionItems";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetCartSubscriptionItemsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}
