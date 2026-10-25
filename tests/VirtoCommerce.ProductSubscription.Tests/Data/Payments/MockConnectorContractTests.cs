using Moq;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.ProductSubscription.ConnectorTestKit;
using VirtoCommerce.ProductSubscription.Data.Payments;
using Xunit;

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
