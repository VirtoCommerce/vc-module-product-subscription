using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class AddSavedPaymentMethodCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<AddSavedPaymentMethodCommand, StoredPaymentMethod, InputAddSavedPaymentMethodType, SavedPaymentMethodType>(authorizationService)
{
    protected override string Name => "addSavedPaymentMethod";
}
