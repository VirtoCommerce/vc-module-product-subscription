using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class ChangeProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<ChangeProductSubscriptionCommand, Subscription, InputChangeSubscriptionType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "changeProductSubscription";
}
