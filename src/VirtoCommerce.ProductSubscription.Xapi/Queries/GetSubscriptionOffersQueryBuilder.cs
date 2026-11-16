using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
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

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetSubscriptionOffersQuery request)
    {
        SubscriptionMoney.RememberCultureName(context);
        await base.BeforeMediatorSend(context, request);
    }
}
