using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetProductSubscriptionsQueryBuilder(IAuthorizationService authorizationService)
    : SearchQueryBuilder<GetProductSubscriptionsQuery, SubscriptionSearchResult, Subscription, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "productSubscriptions";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetProductSubscriptionsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}
