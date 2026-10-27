using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class ResumeProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<ResumeProductSubscriptionCommand, Subscription, InputSubscriptionIdType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "resumeProductSubscription";
}
