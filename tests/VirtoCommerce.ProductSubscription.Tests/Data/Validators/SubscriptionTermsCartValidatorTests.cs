using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using Moq;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Validators;
using VirtoCommerce.XCart.Core.Validators;
using Xunit;
using CartAggregate = VirtoCommerce.XCart.Core.CartAggregate;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Validators;

/// <summary>
/// The server refuses an order with subscription lines when the customer didn't accept the subscription terms, whatever
/// client places it; the cart itself never shows this as an error.
/// </summary>
[Trait("Category", "Unit")]
public class SubscriptionTermsCartValidatorTests
{
    private const string OrderCreate = "orderCreate";

    private readonly Mock<ISubscriptionCartItemSearchService> _choices = new();
    private readonly Mock<IPaymentConsentSearchService> _consents = new();
    private PaymentConsentSearchCriteria _consentCriteria;

    [Fact]
    public async Task SubscribedLineWithoutConsent_RefusesTheOrder()
    {
        SetupChoice("line-1");
        SetupConsents(0);

        var result = await Validate(Cart(new LineItem { Id = "line-1" }), OrderCreate);

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(SubscriptionTermsCartValidator.ErrorCode);
        _consentCriteria.RelatedObjectId.Should().Be("cart-1");
        _consentCriteria.CustomerId.Should().Be("customer-1");
    }

    [Fact]
    public async Task SubscribedLineWithConsent_PlacesTheOrder()
    {
        SetupChoice("line-1");
        SetupConsents(1);

        var result = await Validate(Cart(new LineItem { Id = "line-1" }), OrderCreate);

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task SubscribedLineNotOrdered_NeedsNoConsent()
    {
        SetupChoice("line-later");
        SetupConsents(0);

        var result = await Validate(Cart(new LineItem { Id = "line-1" }, new LineItem { Id = "line-later", SelectedForCheckout = false }), OrderCreate);

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task TheCartItself_NeverShowsTheError()
    {
        SetupChoice("line-1");
        SetupConsents(0);

        var result = await Validate(Cart(new LineItem { Id = "line-1" }), "default");

        result.Errors.Should().BeEmpty();
        _choices.Verify(x => x.SearchAsync(It.IsAny<SubscriptionCartItemSearchCriteria>(), It.IsAny<bool>()), Times.Never);
    }

    private Task<FluentValidation.Results.ValidationResult> Validate(ShoppingCart cart, string ruleSet)
    {
        var validator = new SubscriptionTermsCartValidator(_choices.Object, _consents.Object);
        var context = new CartValidationContext { CartAggregate = new TestCartAggregate(cart) };
        return validator.ValidateAsync(context, options => options.IncludeRuleSets(ruleSet));
    }

    private void SetupChoice(string lineItemId)
    {
        _choices
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionCartItemSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((SubscriptionCartItemSearchCriteria criteria, bool _) => new SubscriptionCartItemSearchResult
            {
                TotalCount = 1,
                Results = criteria.Skip == 0 ? [new SubscriptionCartItem { Id = "choice-1", CartId = "cart-1", LineItemId = lineItemId }] : [],
            });
    }

    private void SetupConsents(int count)
    {
        _consents
            .Setup(x => x.SearchAsync(It.IsAny<PaymentConsentSearchCriteria>(), It.IsAny<bool>()))
            .Callback((PaymentConsentSearchCriteria criteria, bool _) => _consentCriteria = criteria)
            .ReturnsAsync(new PaymentConsentSearchResult { TotalCount = count, Results = new List<PaymentConsent>() });
    }

    private static ShoppingCart Cart(params LineItem[] lines)
    {
        return new ShoppingCart { Id = "cart-1", CustomerId = "customer-1", StoreId = "store-1", Items = lines.ToList() };
    }

    private sealed class TestCartAggregate : CartAggregate
    {
        public TestCartAggregate(ShoppingCart cart)
            : base(null, null, null, null, null, null, null, null, null, null, null, null)
        {
            Cart = cart;
        }
    }
}
