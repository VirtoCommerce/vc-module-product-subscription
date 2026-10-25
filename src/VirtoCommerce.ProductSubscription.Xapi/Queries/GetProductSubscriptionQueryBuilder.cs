using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetProductSubscriptionQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetProductSubscriptionQuery, Subscription, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "productSubscription";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetProductSubscriptionQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}
