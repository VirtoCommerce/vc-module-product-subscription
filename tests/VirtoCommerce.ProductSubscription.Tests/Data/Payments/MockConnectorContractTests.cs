using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.ProductSubscription.ConnectorTestKit;
using VirtoCommerce.ProductSubscription.Data.Payments;
using Xunit;
using TestCards = VirtoCommerce.ProductSubscription.Data.Payments.MockStoredCardPaymentMethod.TestCards;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Payments;

/// <summary>
/// Runs the stored credential connector contract against the mock payment method, so the kit itself is proven
/// against a known connector before Authorize.Net and CyberSource run it against their sandboxes.
/// </summary>
[Trait("Category", "Unit")]
public class MockConnectorContractTests()
    : StoredCredentialConnectorContractTests(
        () => new MockStoredCardPaymentMethod(Mock.Of<IStoredPaymentMethodService>(), Mock.Of<IStoredPaymentMethodSearchService>()),
        new MockConnectorScenarios());

public class MockConnectorScenarios : ConnectorTestScenarios
{
    private static readonly string _expirationYear = (DateTime.UtcNow.Year + 3).ToString(CultureInfo.InvariantCulture);

    public override ChargeScenario Success => new()
    {
        Description = "mock 4242 (always succeeds)",
        Card = Card(TestCards.Success, "Visa"),
        Amount = 25.50m,
    };

    public override ChargeScenario SoftDecline => new()
    {
        Description = "mock 9995 (insufficient funds)",
        Card = Card(TestCards.InsufficientFunds, "Visa"),
        ExpectedRawCode = "51",
        ExpectedAdviceCode = "02",
        ExpectRetryAfter = true,
    };

    // The mock refuses to save 0069, so the card is saved as 4242 and "expires" before the renewal.
    public override ChargeScenario HardDecline => new()
    {
        Description = "mock 0069 (expired after it was saved)",
        Card = Card(TestCards.Success, "Visa"),
        AfterStore = x => x.Last4 = TestCards.Expired,
        ExpectedRawCode = "54",
    };

    public override ChargeScenario HardDeclineWithAdviceCode => new()
    {
        Description = "mock 0021 (recurring payments cancelled, MAC 21)",
        Card = Card(TestCards.RecurringCancelled, "Mastercard"),
        ExpectedRawCode = "05",
        ExpectedAdviceCode = "21",
    };

    public override ChargeScenario RequiresActionOnMerchantInitiatedCharge => new()
    {
        Description = "mock 3184 (3-D Secure on renewal)",
        Card = Card(TestCards.ThreeDsOnRenewal, "Visa"),
    };

    public override AuthenticationScenario ThreeDsOnStore => new()
    {
        Description = "mock 3220 (3-D Secure on save)",
        Card = Card(TestCards.ThreeDsOnSave, "Visa"),
        // The mock storefront form posts threeDsCompleted=true after the simulated challenge.
        CompleteAuthenticationAsync = (request, _, _) => Task.FromResult(new NameValueCollection(request.Parameters) { ["threeDsCompleted"] = "true" }),
    };

    public override CardScenario DeclinedOnStore => Card(TestCards.Expired, "Visa");

    public override ProviderErrorScenario ProviderError => new()
    {
        Description = "mock amount ending in .13 (gateway timeout once per idempotency key)",
        Card = Card(TestCards.Success, "Visa"),
        Amount = 20.13m,
        RetryWithSameKeySucceeds = true,
    };

    // The mock remembers deleted tokens and declines charges against them.
    public override ConnectorCapabilities Capabilities { get; } = new() { RejectsDeletedToken = true };

    // The flag the mock's customer-present flow (PostProcessPaymentAsync) uses for 3-D Secure: code=authentication_required.
    public override IReadOnlyCollection<string> RequiresActionPublicParameters => ["code"];

    // The mock sends nothing on the wire; it echoes the network transaction id it would have referenced.
    public override Task<string> GetReferencedNetworkTransactionIdAsync(ProcessStoredPaymentRequest request, ProcessStoredPaymentRequestResult result, CancellationToken cancellationToken)
    {
        return Task.FromResult(result.NetworkTransactionId);
    }

    private static CardScenario Card(string last4, string brand)
    {
        return new CardScenario
        {
            Description = $"mock {last4}",
            StoreParameters = new NameValueCollection
            {
                ["last4"] = last4,
                ["brand"] = brand,
                ["expMonth"] = "12",
                ["expYear"] = _expirationYear,
                ["cardholderName"] = "Contract Test",
            },
            ExpectedLast4 = last4,
            ExpectedBrand = brand,
        };
    }
}
