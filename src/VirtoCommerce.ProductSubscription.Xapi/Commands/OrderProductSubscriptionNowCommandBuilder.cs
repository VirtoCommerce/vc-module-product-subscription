using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class OrderProductSubscriptionNowCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<OrderProductSubscriptionNowCommand, Renewal, InputSubscriptionIdType, RenewalType>(authorizationService)
{
    protected override string Name => "orderProductSubscriptionNow";
}
