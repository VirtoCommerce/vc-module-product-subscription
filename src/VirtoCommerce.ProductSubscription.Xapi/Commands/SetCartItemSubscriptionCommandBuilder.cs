using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SetCartItemSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<SetCartItemSubscriptionCommand, SubscriptionCartItem, InputSetCartItemSubscriptionType, SubscriptionCartItemType>(authorizationService)
{
    protected override string Name => "setCartItemSubscription";
}
