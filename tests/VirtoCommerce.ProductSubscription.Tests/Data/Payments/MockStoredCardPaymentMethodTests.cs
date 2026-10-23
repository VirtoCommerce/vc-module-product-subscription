using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Data.Payments;
using Xunit;
using TestCards = VirtoCommerce.ProductSubscription.Data.Payments.MockStoredCardPaymentMethod.TestCards;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Payments;

[Trait("Category", "Unit")]
public class MockStoredCardPaymentMethodTests
{
    private readonly Mock<IStoredPaymentMethodService> _storedPaymentMethodServiceMock = new();
    private readonly Mock<IStoredPaymentMethodSearchService> _storedPaymentMethodSearchServiceMock = new();

    private MockStoredCardPaymentMethod CreateSut() => new(_storedPaymentMethodServiceMock.Object, _storedPaymentMethodSearchServiceMock.Object);

    // StorePaymentMethodAsync (customer saves a card)

    [Fact]
    public async Task StorePaymentMethodAsync_SuccessCard_BuildsStoredMethodWithoutPersistingIt()
    {
        // Arrange
        var request = CreateStoreRequest(TestCards.Success, new NameValueCollection
        {
            ["brand"] = "Visa",
            ["expMonth"] = "12",
            ["expYear"] = "29",
            ["cardholderName"] = "Jane Doe",
        });

        // Act
        var result = await CreateSut().StorePaymentMethodAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);
        result.ErrorMessage.Should().BeNull();

        var method = result.StoredPaymentMethod;
        method.Should().NotBeNull();
        method.StoreId.Should().Be("store-1");
        method.CustomerId.Should().Be("customer-1");
        method.OrganizationId.Should().Be("org-1");
        method.PaymentMethodCode.Should().Be(ModuleConstants.MockPaymentMethodCode);
        method.ProviderCustomerRef.Should().Be("mock_cus_customer-1");
        method.Token.Should().StartWith("mock_tok_");
        method.Brand.Should().Be("Visa");
        method.Last4.Should().Be("4242");
        method.ExpirationMonth.Should().Be(12);
        method.ExpirationYear.Should().Be(2029);
        method.CardholderName.Should().Be("Jane Doe");
        method.Fingerprint.Should().Be("mock_fp_4242");
        method.NetworkTransactionId.Should().StartWith("mock_ntid_");
        method.Usage.Should().Be(StoredCredentialUsage.Unscheduled);
        method.ConsentId.Should().Be("consent-1");
        method.IsAuthenticated.Should().BeFalse();

        _storedPaymentMethodServiceMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("4", "Visa")]
    [InlineData("5", "Mastercard")]
    [InlineData("2", "Mastercard")]
    [InlineData("3", "American Express")]
    [InlineData("6", "Discover")]
    [InlineData(null, "Card")]
    public async Task StorePaymentMethodAsync_NoBrand_GuessesFromFirstDigit(string firstDigit, string expectedBrand)
    {
        // Arrange
        var request = CreateStoreRequest(TestCards.Success, new NameValueCollection { ["firstDigit"] = firstDigit });

        // Act
        var result = await CreateSut().StorePaymentMethodAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.StoredPaymentMethod.Brand.Should().Be(expectedBrand);
    }

    [Fact]
    public async Task StorePaymentMethodAsync_FourDigitYearAndBadMonth_AreNormalized()
    {
        // Arrange
        var request = CreateStoreRequest(TestCards.Success, new NameValueCollection { ["expMonth"] = "xx", ["expYear"] = "2031" });

        // Act
        var result = await CreateSut().StorePaymentMethodAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.StoredPaymentMethod.ExpirationMonth.Should().BeNull();
        result.StoredPaymentMethod.ExpirationYear.Should().Be(2031);
    }

    [Fact]
    public async Task StorePaymentMethodAsync_Last4WithSeparators_UsesDigitsOnly()
    {
        // Act
        var result = await CreateSut().StorePaymentMethodAsync(CreateStoreRequest("42-42"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StoredPaymentMethod.Last4.Should().Be("4242");
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("4242424242424242")]
    [InlineData("abcd")]
    public async Task StorePaymentMethodAsync_NotFourDigits_HardDecline(string last4)
    {
        // Act
        var result = await CreateSut().StorePaymentMethodAsync(CreateStoreRequest(last4), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.HardDecline);
        result.ErrorMessage.Should().Be("Enter a valid test card number.");
        result.StoredPaymentMethod.Should().BeNull();
    }

    [Fact]
    public async Task StorePaymentMethodAsync_NullParameters_HardDecline()
    {
        // Arrange
        var request = new StorePaymentMethodRequest { StoreId = "store-1", CustomerId = "customer-1", Parameters = null };

        // Act
        var result = await CreateSut().StorePaymentMethodAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.HardDecline);
    }

    [Fact]
    public async Task StorePaymentMethodAsync_ThreeDsOnSaveCard_WithoutAuthentication_RequiresAction()
    {
        // Act
        var result = await CreateSut().StorePaymentMethodAsync(CreateStoreRequest(TestCards.ThreeDsOnSave), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.RequiresAction);
        result.RawCode.Should().Be("authentication_required");
        result.ErrorMessage.Should().Contain("3-D Secure");
        result.StoredPaymentMethod.Should().BeNull();
    }

    [Fact]
    public async Task StorePaymentMethodAsync_ThreeDsOnSaveCard_AfterAuthentication_SucceedsAndIsAuthenticated()
    {
        // Arrange
        var request = CreateStoreRequest(TestCards.ThreeDsOnSave, new NameValueCollection { ["threeDsCompleted"] = "TRUE" });

        // Act
        var result = await CreateSut().StorePaymentMethodAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);
        result.StoredPaymentMethod.Last4.Should().Be(TestCards.ThreeDsOnSave);
        result.StoredPaymentMethod.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task StorePaymentMethodAsync_ExpiredCard_HardDecline()
    {
        // Act
        var result = await CreateSut().StorePaymentMethodAsync(CreateStoreRequest(TestCards.Expired), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.HardDecline);
        result.RawCode.Should().Be("54");
        result.ErrorMessage.Should().Be("Your card has expired.");
        result.StoredPaymentMethod.Should().BeNull();
    }

    [Theory]
    [InlineData(TestCards.ThreeDsOnRenewal)]
    [InlineData(TestCards.InsufficientFunds)]
    [InlineData(TestCards.RecurringCancelled)]
    public async Task StorePaymentMethodAsync_CardsThatOnlyFailOnRenewal_SaveSuccessfully(string last4)
    {
        // Act
        var result = await CreateSut().StorePaymentMethodAsync(CreateStoreRequest(last4), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);
        result.StoredPaymentMethod.Last4.Should().Be(last4);
    }

    // ProcessStoredPaymentAsync (merchant-initiated renewal charge)

    [Fact]
    public async Task ProcessStoredPaymentAsync_NullRequest_Throws()
    {
        // Act
        var act = () => CreateSut().ProcessStoredPaymentAsync(null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_SuccessCard_AuthorizeOnly_Authorizes()
    {
        // Arrange
        var request = CreateChargeRequest(TestCards.Success, 25.50m);

        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);
        result.NewPaymentStatus.Should().Be(PaymentStatus.Authorized);
        result.TransactionId.Should().StartWith("mock_txn_");
        result.NetworkTransactionId.Should().Be("ntid-original");
        result.AuthorizationCode.Should().MatchRegex("^MOCK[0-9]{6}$");
        result.Amount.Should().Be(25.50m);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_SuccessCard_AuthorizeAndCapture_IsPaid()
    {
        // Arrange
        var request = CreateChargeRequest(TestCards.Success, 25.50m);
        request.CaptureMode = StoredPaymentCaptureMode.AuthorizeAndCapture;

        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.NewPaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_ThreeDsOnSaveCard_RenewalSucceeds()
    {
        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.ThreeDsOnSave, 10m), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);
    }

    [Theory]
    [InlineData(MerchantInitiatedReason.Recurring)]
    [InlineData(MerchantInitiatedReason.Resubmission)]
    [InlineData(MerchantInitiatedReason.Unscheduled)]
    [InlineData(MerchantInitiatedReason.DelayedCharge)]
    public async Task ProcessStoredPaymentAsync_ThreeDsOnRenewalCard_RequiresAction(MerchantInitiatedReason reason)
    {
        // Arrange
        var request = CreateChargeRequest(TestCards.ThreeDsOnRenewal, 10m);
        request.Reason = reason;

        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.RequiresAction);
        result.RawCode.Should().Be("1A");
        result.TransactionId.Should().BeNull();
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_ThreeDsOnRenewalCard_Reauthorization_Succeeds()
    {
        // Arrange
        var request = CreateChargeRequest(TestCards.ThreeDsOnRenewal, 10m);
        request.Reason = MerchantInitiatedReason.Reauthorization;

        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_InsufficientFundsCard_SoftDeclineWithRetryAfter()
    {
        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.InsufficientFunds, 10m), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.SoftDecline);
        result.RawCode.Should().Be("51");
        result.AdviceCode.Should().Be("02");
        result.RetryAfter.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_ExpiredCard_HardDecline()
    {
        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Expired, 10m), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.HardDecline);
        result.RawCode.Should().Be("54");
        result.AdviceCode.Should().BeNull();
        result.RetryAfter.Should().BeNull();
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_RecurringCancelledCard_HardDeclineWithAdviceCode21()
    {
        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.RecurringCancelled, 10m), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.HardDecline);
        result.RawCode.Should().Be("05");
        result.AdviceCode.Should().Be("21");
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_AmountEndingIn13_ProviderErrorOnce_ThenRetryWithSameKeySucceeds()
    {
        // Arrange
        var sut = CreateSut();
        var key = NewKey();

        // Act
        var first = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, 20.13m, key), TestContext.Current.CancellationToken);
        var retry = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, 20.13m, key), TestContext.Current.CancellationToken);

        // Assert
        first.IsSuccess.Should().BeFalse();
        first.Outcome.Should().Be(ChargeOutcome.ProviderError);
        first.RawCode.Should().Be("timeout");

        retry.IsSuccess.Should().BeTrue();
        retry.Outcome.Should().Be(ChargeOutcome.Succeeded);
        retry.Amount.Should().Be(20.13m);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_AmountEndingIn13_TakesPrecedenceOverCardOutcome()
    {
        // Arrange
        var sut = CreateSut();
        var key = NewKey();

        // Act
        var first = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.InsufficientFunds, 0.13m, key), TestContext.Current.CancellationToken);
        var retry = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.InsufficientFunds, 0.13m, key), TestContext.Current.CancellationToken);

        // Assert
        first.Outcome.Should().Be(ChargeOutcome.ProviderError);
        retry.Outcome.Should().Be(ChargeOutcome.SoftDecline);
    }

    [Theory]
    [InlineData(1.3)]
    [InlineData(113)]
    [InlineData(10.31)]
    public async Task ProcessStoredPaymentAsync_AmountNotEndingInDot13_NoProviderError(decimal amount)
    {
        // Act
        var result = await CreateSut().ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, amount), TestContext.Current.CancellationToken);

        // Assert
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_SameIdempotencyKey_ReplaysFirstResult()
    {
        // Arrange
        var sut = CreateSut();
        var key = NewKey();

        // Act
        var first = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, 10m, key), TestContext.Current.CancellationToken);
        // A replay with the same key must not charge again, even if the request differs.
        var replay = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Expired, 99m, key), TestContext.Current.CancellationToken);

        // Assert
        replay.Should().BeSameAs(first);
        replay.Outcome.Should().Be(ChargeOutcome.Succeeded);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_SameIdempotencyKey_ReplaysDeclines()
    {
        // Arrange
        var sut = CreateSut();
        var key = NewKey();

        // Act
        var first = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.InsufficientFunds, 10m, key), TestContext.Current.CancellationToken);
        var replay = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, 10m, key), TestContext.Current.CancellationToken);

        // Assert
        replay.Should().BeSameAs(first);
        replay.Outcome.Should().Be(ChargeOutcome.SoftDecline);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_DifferentIdempotencyKeys_ChargeIndependently()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var first = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, 10m), TestContext.Current.CancellationToken);
        var second = await sut.ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, 10m), TestContext.Current.CancellationToken);

        // Assert
        second.Should().NotBeSameAs(first);
        second.TransactionId.Should().NotBe(first.TransactionId);
    }

    [Fact]
    public async Task ProcessStoredPaymentAsync_DoesNotTouchStoredPaymentMethodServices()
    {
        // Act
        await CreateSut().ProcessStoredPaymentAsync(CreateChargeRequest(TestCards.Success, 10m), TestContext.Current.CancellationToken);

        // Assert
        _storedPaymentMethodServiceMock.VerifyNoOtherCalls();
        _storedPaymentMethodSearchServiceMock.VerifyNoOtherCalls();
    }

    // Method shape

    [Fact]
    public void PaymentMethod_IsStoredCardMethodThatCannotPayCarts()
    {
        // Act
        var sut = CreateSut();

        // Assert
        sut.Code.Should().Be(ModuleConstants.MockPaymentMethodCode);
        sut.Name.Should().Be("Credit card (test)");
        sut.AllowCartPayment.Should().BeFalse();
        sut.PaymentMethodType.Should().Be(PaymentMethodType.Standard);
        sut.PaymentMethodGroupType.Should().Be(PaymentMethodGroupType.BankCard);
        sut.Should().BeAssignableTo<ISupportStoredCredentialFlow>();
    }

    private static StorePaymentMethodRequest CreateStoreRequest(string last4, NameValueCollection extraParameters = null)
    {
        var parameters = new NameValueCollection { ["last4"] = last4 };
        if (extraParameters != null)
        {
            parameters.Add(extraParameters);
        }

        return new StorePaymentMethodRequest
        {
            StoreId = "store-1",
            CustomerId = "customer-1",
            OrganizationId = "org-1",
            ConsentId = "consent-1",
            Usage = StoredCredentialUsage.Unscheduled,
            Parameters = parameters,
        };
    }

    private static ProcessStoredPaymentRequest CreateChargeRequest(string last4, decimal amount, string idempotencyKey = null)
    {
        return new ProcessStoredPaymentRequest
        {
            StoredPaymentMethod = new StoredPaymentMethod
            {
                Id = "spm-1",
                CustomerId = "customer-1",
                Last4 = last4,
                Token = "mock_tok_test",
                NetworkTransactionId = "ntid-original",
            },
            Amount = amount,
            Currency = "USD",
            IdempotencyKey = idempotencyKey ?? NewKey(),
        };
    }

    // The mock keeps its idempotency cache in static state, so every test uses its own keys.
    private static string NewKey() => $"test-{Guid.NewGuid():N}";
}
