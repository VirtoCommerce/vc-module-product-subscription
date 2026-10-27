using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionRenewalQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSubscriptionRenewalQuery, Renewal, RenewalType>(authorizationService)
{
    protected override string Name => "subscriptionRenewal";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetSubscriptionRenewalQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}
