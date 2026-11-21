using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class AcceptSubscriptionRenewalCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<AcceptSubscriptionRenewalCommand, Renewal, InputRenewalIdType, RenewalType>(authorizationService)
{
    protected override string Name => "acceptSubscriptionRenewal";
}
