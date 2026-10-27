using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class AcceptSubscriptionTermsCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<AcceptSubscriptionTermsCommand, string, InputAcceptSubscriptionTermsType, StringGraphType>(authorizationService)
{
    protected override string Name => "acceptSubscriptionTerms";
}
