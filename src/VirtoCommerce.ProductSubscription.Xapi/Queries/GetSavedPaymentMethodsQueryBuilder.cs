using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSavedPaymentMethodsQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSavedPaymentMethodsQuery, IList<StoredPaymentMethod>, NonNullGraphType<ListGraphType<NonNullGraphType<SavedPaymentMethodType>>>>(authorizationService)
{
    protected override string Name => "savedPaymentMethods";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetSavedPaymentMethodsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}
