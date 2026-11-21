using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class RemoveCartItemSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<RemoveCartItemSubscriptionCommand, bool, InputRemoveCartItemSubscriptionType, BooleanGraphType>(authorizationService)
{
    protected override string Name => "removeCartItemSubscription";
}
