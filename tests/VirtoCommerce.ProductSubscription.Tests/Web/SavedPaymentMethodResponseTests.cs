using FluentAssertions;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.ProductSubscription.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Web;

/// <summary>The back office lists a customer's cards; what could charge or track a card stays on the server.</summary>
[Trait("Category", "Unit")]
public class SavedPaymentMethodResponseTests
{
    [Fact]
    public void WithoutSecrets_KeepsWhatTheBackOfficeShows_AndDropsTheProviderReferences()
    {
        var card = new StoredPaymentMethod
        {
            Id = "card-1",
            Brand = "Visa",
            Last4 = "4242",
            ExpirationMonth = 12,
            ExpirationYear = 2029,
            Status = StoredPaymentMethodStatus.Active,
            IsDefault = true,
            Token = "tok_secret",
            ProviderCustomerRef = "cus_secret",
            Fingerprint = "fp_secret",
            NetworkTransactionId = "ntid_secret",
            InitialTransactionRef = "txn_secret",
        };

        var result = ProductSubscriptionController.WithoutSecrets(card);

        result.Should().BeEquivalentTo(new { Id = "card-1", Brand = "Visa", Last4 = "4242", ExpirationMonth = 12, ExpirationYear = 2029, IsDefault = true });
        result.Token.Should().BeNull();
        result.ProviderCustomerRef.Should().BeNull();
        result.Fingerprint.Should().BeNull();
        result.NetworkTransactionId.Should().BeNull();
        result.InitialTransactionRef.Should().BeNull();
        card.Token.Should().Be("tok_secret", "the stored card itself is not changed");
    }
}
