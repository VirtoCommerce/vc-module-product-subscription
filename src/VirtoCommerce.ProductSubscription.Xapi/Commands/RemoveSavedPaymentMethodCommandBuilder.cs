using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class RemoveSavedPaymentMethodCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<RemoveSavedPaymentMethodCommand, StoredPaymentMethod, InputSavedPaymentMethodIdType, SavedPaymentMethodType>(authorizationService)
{
    protected override string Name => "removeSavedPaymentMethod";
}
