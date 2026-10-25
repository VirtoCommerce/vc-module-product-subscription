using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SkipProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<SkipProductSubscriptionCommand, Subscription, InputSubscriptionIdType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "skipProductSubscription";
}
