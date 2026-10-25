using System.Collections.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionOffersQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSubscriptionOffersQuery, IList<SubscriptionOffer>, NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionOfferType>>>>(authorizationService)
{
    protected override string Name => "subscriptionOffers";
}
