using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class CancelProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<CancelProductSubscriptionCommand, Subscription, InputCancelSubscriptionType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "cancelProductSubscription";
}
