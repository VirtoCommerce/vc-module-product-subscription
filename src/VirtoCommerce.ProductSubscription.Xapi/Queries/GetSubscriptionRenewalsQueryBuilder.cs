using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionRenewalsQueryBuilder(IAuthorizationService authorizationService)
    : SearchQueryBuilder<GetSubscriptionRenewalsQuery, RenewalSearchResult, Renewal, RenewalType>(authorizationService)
{
    protected override string Name => "subscriptionRenewals";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetSubscriptionRenewalsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}
