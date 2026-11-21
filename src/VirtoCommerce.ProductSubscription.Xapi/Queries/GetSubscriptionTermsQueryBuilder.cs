using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionTermsQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSubscriptionTermsQuery, SubscriptionTerms, SubscriptionTermsType>(authorizationService)
{
    protected override string Name => "subscriptionTerms";
}
