using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class RetrySubscriptionRenewalPaymentCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<RetrySubscriptionRenewalPaymentCommand, Renewal, InputRenewalIdType, RenewalType>(authorizationService)
{
    protected override string Name => "retrySubscriptionRenewalPayment";
}
