using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SetDefaultSavedPaymentMethodCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<SetDefaultSavedPaymentMethodCommand, StoredPaymentMethod, InputSavedPaymentMethodIdType, SavedPaymentMethodType>(authorizationService)
{
    protected override string Name => "setDefaultSavedPaymentMethod";
}
