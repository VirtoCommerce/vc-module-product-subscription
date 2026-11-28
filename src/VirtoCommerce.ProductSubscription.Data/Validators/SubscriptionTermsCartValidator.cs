using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Validators;
using XCartModuleConstants = VirtoCommerce.XCart.Core.ModuleConstants;

namespace VirtoCommerce.ProductSubscription.Data.Validators;

/// <summary>
/// An order with subscription lines needs the customer's consent to the subscription terms (US auto-renewal rules). The
/// storefront keeps Place order closed until the terms are accepted; this check makes the server refuse such an order
/// too, whatever client places it. It runs only when an order is created (xCart's "orderCreate" rule set), so the cart
/// itself never shows it as an error.
/// </summary>
public class SubscriptionTermsCartValidator : AbstractValidator<CartValidationContext>, ICartValidator<CartValidationContext>
{
    public const string ErrorCode = "SUBSCRIPTION_TERMS_NOT_ACCEPTED";

    private readonly ISubscriptionCartItemSearchService _cartItemSearchService;
    private readonly IPaymentConsentSearchService _consentSearchService;
    private readonly ISubscriptionTermsService _termsService;

    public SubscriptionTermsCartValidator(
        ISubscriptionCartItemSearchService cartItemSearchService,
        IPaymentConsentSearchService consentSearchService,
        ISubscriptionTermsService termsService)
    {
        _cartItemSearchService = cartItemSearchService;
        _consentSearchService = consentSearchService;
        _termsService = termsService;

        RuleSet(XCartModuleConstants.ValidationRuleSets.OrderCreate, () =>
            RuleFor(x => x).CustomAsync(ValidateConsentAsync));
    }

    public int Order => 0;

    protected virtual async Task ValidateConsentAsync(CartValidationContext cartContext, ValidationContext<CartValidationContext> context, CancellationToken cancellationToken)
    {
        var cart = cartContext.CartAggregate?.Cart;
        if (string.IsNullOrEmpty(cart?.Id) || !await HasSubscriptionLinesAsync(cart))
        {
            return;
        }

        if (!await HasConsentAsync(cart))
        {
            context.AddFailure(new CartValidationError(cart, "Please accept the subscription terms to place this order.", ErrorCode));
        }
    }

    protected virtual async Task<bool> HasSubscriptionLinesAsync(ShoppingCart cart)
    {
        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.Take = ModuleConstants.SearchPageSize;
        var choices = await _cartItemSearchService.SearchAllNoCloneAsync(criteria);
        if (choices.Count == 0)
        {
            return false;
        }

        var orderedLineIds = (cart.Items ?? [])
            .Where(x => x.SelectedForCheckout && !x.IsGift)
            .Select(x => x.Id)
            .ToHashSet();

        return choices.Any(x => orderedLineIds.Contains(x.LineItemId));
    }

    /// <summary>
    /// A consent given for this checkout (the cart's CheckoutId, which xOrder replaces after each order), not revoked, to
    /// the store's current terms: a consent from an earlier order, or to terms that have changed since, doesn't count.
    /// </summary>
    protected virtual async Task<bool> HasConsentAsync(ShoppingCart cart)
    {
        var criteria = AbstractTypeFactory<PaymentConsentSearchCriteria>.TryCreateInstance();
        criteria.CustomerId = cart.CustomerId;
        criteria.RelatedObjectType = ModuleConstants.ConsentObjectType;
        criteria.RelatedObjectId = string.IsNullOrEmpty(cart.CheckoutId) ? cart.Id : cart.CheckoutId;
        criteria.Purpose = ModuleConstants.ConsentPurpose;
        criteria.Take = ModuleConstants.SearchPageSize;

        var consents = (await _consentSearchService.SearchAsync(criteria))?.Results ?? [];
        if (consents.Count == 0)
        {
            return false;
        }

        var terms = await _termsService.GetTermsAsync(cart.StoreId);
        return consents.Any(x => x.RevokedDate == null && (string.IsNullOrEmpty(terms?.Hash) || x.TermsHash == terms.Hash));
    }
}
