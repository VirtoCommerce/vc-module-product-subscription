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
    private readonly Mock<ISubscriptionTermsService> _terms = new();
    private PaymentConsentSearchCriteria _consentCriteria;

    public SubscriptionTermsCartValidatorTests()
    {
        _terms.Setup(x => x.GetTermsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new SubscriptionTerms { Version = "v2", Hash = "hash-v2", Text = "Terms" });
    }

    [Fact]
    public async Task SubscribedLineWithoutConsent_RefusesTheOrder()
    {
        SetupChoice("line-1");
        SetupConsents(0);

        var result = await Validate(Cart(new LineItem { Id = "line-1" }), OrderCreate);

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(SubscriptionTermsCartValidator.ErrorCode);
        _consentCriteria.RelatedObjectType.Should().Be("ShoppingCartCheckout");
        _consentCriteria.RelatedObjectId.Should().Be("checkout-1");
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

    // VCST-6993: xOrder gives the cart a new CheckoutId after each order, so a consent for an earlier checkout doesn't pass
    [Fact]
    public async Task ConsentForAnEarlierCheckout_RefusesTheOrder()
    {
        SetupChoice("line-1");
        SetupConsents(1, checkoutId: "checkout-earlier");

        var result = await Validate(Cart(new LineItem { Id = "line-1" }), OrderCreate);

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(SubscriptionTermsCartValidator.ErrorCode);
    }

    [Fact]
    public async Task ConsentToTermsThatHaveSinceChanged_RefusesTheOrder()
    {
        SetupChoice("line-1");
        SetupConsents(1, termsHash: "hash-v1");

        var result = await Validate(Cart(new LineItem { Id = "line-1" }), OrderCreate);

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(SubscriptionTermsCartValidator.ErrorCode);
    }

    [Fact]
    public async Task RevokedConsent_RefusesTheOrder()
    {
        SetupChoice("line-1");
        SetupConsents(1, revoked: true);

        var result = await Validate(Cart(new LineItem { Id = "line-1" }), OrderCreate);

        result.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(SubscriptionTermsCartValidator.ErrorCode);
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
        var validator = new SubscriptionTermsCartValidator(_choices.Object, _consents.Object, _terms.Object);
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

    /// <summary>The customer's consents, each given for <paramref name="checkoutId"/>; the search filters by checkout.</summary>
    private void SetupConsents(int count, string checkoutId = "checkout-1", string termsHash = "hash-v2", bool revoked = false)
    {
        var consents = Enumerable.Range(1, count)
            .Select(i => new PaymentConsent { Id = $"consent-{i}", ObjectId = checkoutId, TermsHash = termsHash, RevokedDate = revoked ? System.DateTime.UtcNow : null })
            .ToList();
        _consents
            .Setup(x => x.SearchAsync(It.IsAny<PaymentConsentSearchCriteria>(), It.IsAny<bool>()))
            .Callback((PaymentConsentSearchCriteria criteria, bool _) => _consentCriteria = criteria)
            .ReturnsAsync((PaymentConsentSearchCriteria criteria, bool _) =>
            {
                var found = consents.Where(x => x.ObjectId == criteria.RelatedObjectId).ToList();
                return new PaymentConsentSearchResult { TotalCount = found.Count, Results = found };
            });
    }

    private static ShoppingCart Cart(params LineItem[] lines)
    {
        return new ShoppingCart { Id = "cart-1", CheckoutId = "checkout-1", CustomerId = "customer-1", StoreId = "store-1", Items = lines.ToList() };
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
