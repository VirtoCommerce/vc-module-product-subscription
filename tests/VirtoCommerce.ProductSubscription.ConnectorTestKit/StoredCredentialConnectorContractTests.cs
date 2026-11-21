using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;
using Xunit;

namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

/// <summary>
/// Contract tests every payment connector that implements <see cref="ISupportStoredCredentialFlow"/> must pass.
/// Derive a test class in the connector's test project, pass a factory for the connector and the scenarios that make
/// its sandbox produce each outcome. Scenarios the sandbox can't simulate are reported as skipped.
/// See README.md for setup and for the card network requirement each test covers.
/// </summary>
public abstract class StoredCredentialConnectorContractTests : IAsyncDisposable
{
    private readonly Func<PaymentMethod> _connectorFactory;
    private readonly List<StoredPaymentMethod> _storedMethods = [];
    private readonly HashSet<StoredPaymentMethod> _deletedMethods = [];
    private PaymentMethod _connector;

    protected StoredCredentialConnectorContractTests(Func<PaymentMethod> connectorFactory, IConnectorTestScenarios scenarios)
    {
        _connectorFactory = connectorFactory ?? throw new ArgumentNullException(nameof(connectorFactory));
        Scenarios = scenarios ?? throw new ArgumentNullException(nameof(scenarios));
    }

    protected IConnectorTestScenarios Scenarios { get; }

    protected ConnectorCapabilities Capabilities => Scenarios.Capabilities ?? new ConnectorCapabilities();

    protected PaymentMethod Connector => _connector ??= _connectorFactory() ?? throw new InvalidOperationException("The connector factory returned null.");

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // Connector shape

    [Fact]
    public void Connector_ImplementsStoredCredentialFlow()
    {
        Begin();

        Connector.Should().BeAssignableTo<ISupportStoredCredentialFlow>("the platform only uses stored credentials with connectors that declare the flow");

        // The PaymentMethod defaults throw NotImplementedException; a connector that declares the flow must override all three.
        var type = Connector.GetType();
        Overrides(type, nameof(PaymentMethod.StorePaymentMethodAsync), typeof(StorePaymentMethodRequest)).Should().BeTrue("StorePaymentMethodAsync must be overridden");
        Overrides(type, nameof(PaymentMethod.ProcessStoredPaymentAsync), typeof(ProcessStoredPaymentRequest)).Should().BeTrue("ProcessStoredPaymentAsync must be overridden");
        Overrides(type, nameof(PaymentMethod.DeleteStoredPaymentMethodAsync), typeof(DeleteStoredPaymentMethodRequest)).Should().BeTrue("DeleteStoredPaymentMethodAsync must be overridden, even if it only reports that deletion isn't supported");
    }

    [Fact]
    public void Connector_CaptureSupport_MatchesCaptureFlowDeclaration()
    {
        Begin();

        var type = Connector.GetType();
        if (Capabilities.SupportsCapture)
        {
            Connector.Should().BeAssignableTo<ISupportCaptureFlow>("authorized renewals are captured through the capture flow when the order ships");
            Overrides(type, nameof(PaymentMethod.CaptureProcessPaymentAsync), typeof(CapturePaymentRequest)).Should().BeTrue("CaptureProcessPaymentAsync must be overridden");
        }
        else
        {
            Connector.Should().NotBeAssignableTo<ISupportCaptureFlow>("a connector that can't capture must not declare the capture flow");
            Capabilities.SupportsAuthorizeOnly.Should().BeFalse("an authorization that can never be captured is useless; declare SupportsAuthorizeOnly = false");
        }
    }

    // Storing a card (customer-initiated, first transaction)

    [Fact]
    public async Task Store_SuccessfulCard_ReturnsTokenCardDetailsAndInitialTransactionReference()
    {
        Begin();
        var card = RequireSuccessCard();

        var (_, result) = await StoreAsync(card);

        result.IsSuccess.Should().BeTrue("{0} must be saved; error: {1} ({2})", card, result.ErrorMessage, result.RawCode);
        result.Outcome.Should().Be(ChargeOutcome.Succeeded);

        var method = result.StoredPaymentMethod;
        method.Should().NotBeNull("a successful store returns the stored payment method for the caller to persist");
        method.Token.Should().NotBeNullOrWhiteSpace("merchant-initiated charges need the provider token");
        method.Last4.Should().MatchRegex("^[0-9]{4}$");
        if (card.ExpectedLast4 != null)
        {
            method.Last4.Should().Be(card.ExpectedLast4);
        }

        method.Brand.Should().NotBeNullOrWhiteSpace("customers pick saved cards by brand and last 4 digits");
        if (card.ExpectedBrand != null)
        {
            method.Brand.Should().BeEquivalentTo(card.ExpectedBrand);
        }

        method.ExpirationMonth.Should().BeInRange(1, 12, "the platform uses the expiry to warn about cards that will expire before a renewal");
        method.ExpirationYear.Should().BeGreaterThanOrEqualTo(DateTime.UtcNow.Year, "the expiration year is a four-digit year that isn't in the past");

        (method.NetworkTransactionId ?? method.InitialTransactionRef).Should().NotBeNullOrWhiteSpace(
            "merchant-initiated charges must reference the first (customer-initiated) transaction: return NetworkTransactionId or InitialTransactionRef");

        if (card.CardNumber != null)
        {
            var digits = new string(card.CardNumber.Where(char.IsDigit).ToArray());
            StringValues(method).Should().NotContain(x => x.Contains(digits), "the full card number must never be stored in Virto");
        }
    }

    [Fact]
    public async Task Store_SuccessfulCard_KeepsConsentUsageAndConnectorCode()
    {
        Begin();
        var card = RequireSuccessCard();

        var (request, result) = await StoreAsync(card, x => x.Usage = StoredCredentialUsage.Unscheduled);

        result.IsSuccess.Should().BeTrue("{0} must be saved; error: {1}", card, result.ErrorMessage);
        var method = result.StoredPaymentMethod;
        method.PaymentMethodCode.Should().Be(Connector.Code, "the platform finds the connector that owns a token by PaymentMethodCode (e.g. to delete it)");
        method.ConsentId.Should().Be(request.ConsentId, "the stored credential must be linked to the customer's consent to store it");
        method.Usage.Should().Be(request.Usage, "the agreed usage (recurring or unscheduled) decides how later charges are flagged");
    }

    [Fact]
    public async Task Store_ThreeDsCard_RequiresActionThenSucceedsAfterAuthentication()
    {
        Begin();
        var scenario = Require(Scenarios.ThreeDsOnStore, "a card that requires 3-D Secure to be saved");

        var (request, first) = await StoreAsync(scenario.Card);

        first.IsSuccess.Should().BeFalse();
        first.Outcome.Should().Be(ChargeOutcome.RequiresAction, "{0} must ask for authentication before it is saved", scenario);
        first.StoredPaymentMethod.Should().BeNull("nothing is stored until the customer authenticates");

        if (scenario.CompleteAuthenticationAsync == null)
        {
            Assert.Skip("The scenario doesn't say how to complete 3-D Secure, so storage after authentication isn't tested.");
        }

        var parameters = await scenario.CompleteAuthenticationAsync(request, first, CancellationToken);
        var (_, second) = await StoreAsync(scenario.Card, x => x.Parameters = parameters);

        second.IsSuccess.Should().BeTrue("the card must be saved once 3-D Secure is completed; error: {0} ({1})", second.ErrorMessage, second.RawCode);
        second.Outcome.Should().Be(ChargeOutcome.Succeeded);
        second.StoredPaymentMethod.Should().NotBeNull();
        second.StoredPaymentMethod.Token.Should().NotBeNullOrWhiteSpace();
        second.StoredPaymentMethod.IsAuthenticated.Should().BeTrue("the stored method records that the first transaction was authenticated");
    }

    [Fact]
    public async Task Store_RequiresAction_GivesStorefrontARecoveryPath()
    {
        Begin();
        var scenario = Require(Scenarios.ThreeDsOnStore, "a card that requires 3-D Secure to be saved");

        var (_, result) = await StoreAsync(scenario.Card);

        result.Outcome.Should().Be(ChargeOutcome.RequiresAction);
        AssertRecoveryPath(result);
    }

    [Fact]
    public async Task Store_DeclinedCard_IsNotStored()
    {
        Begin();
        var card = Require(Scenarios.DeclinedOnStore, "a card the provider refuses to save");

        var (_, result) = await StoreAsync(card);

        result.IsSuccess.Should().BeFalse("{0} must not be saved", card);
        result.Outcome.Should().BeOneOf([ChargeOutcome.HardDecline, ChargeOutcome.SoftDecline], "a refused verification is a decline");
        result.StoredPaymentMethod.Should().BeNull("a declined card must not be stored");
        result.RawCode.Should().NotBeNullOrWhiteSpace("declines keep the provider code for support and dispute handling");
    }

    // Merchant-initiated charges

    [Fact]
    public async Task Charge_Subsequent_ReferencesOriginalNetworkTransactionId()
    {
        Begin();
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);
        var originalReference = stored.NetworkTransactionId ?? stored.InitialTransactionRef;
        originalReference.Should().NotBeNullOrWhiteSpace("the stored method must reference the first transaction");

        var firstRequest = CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode);
        var first = await ChargeAsync(firstRequest);
        first.Outcome.Should().Be(ChargeOutcome.Succeeded, "error: {0} ({1})", first.RawMessage, first.RawCode);

        var referenced = await Scenarios.GetReferencedNetworkTransactionIdAsync(firstRequest, first, CancellationToken);
        if (referenced == null)
        {
            Assert.Skip("The scenarios can't observe which network transaction id the connector sent.");
        }

        referenced.Should().Be(originalReference, "a merchant-initiated charge (Sequence = Subsequent) references the first customer-initiated transaction");

        // Later charges keep referencing the original transaction, not the previous charge.
        var secondRequest = CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode);
        var second = await ChargeAsync(secondRequest);
        second.Outcome.Should().Be(ChargeOutcome.Succeeded);
        var referencedAgain = await Scenarios.GetReferencedNetworkTransactionIdAsync(secondRequest, second, CancellationToken);
        referencedAgain.Should().Be(originalReference, "every subsequent charge references the original transaction");
    }

    [Fact]
    public async Task Charge_AuthorizeOnly_IsAuthorizedWithTransactionAndAmount()
    {
        Begin();
        RequireCapability(Capabilities.SupportsAuthorizeOnly, "authorize-only charges");
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);

        var result = await ChargeAsync(CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, StoredPaymentCaptureMode.AuthorizeOnly));

        AssertSucceeded(result, scenario.Amount);
        result.NewPaymentStatus.Should().Be(PaymentStatus.Authorized, "AuthorizeOnly must not capture the money");
    }

    [Fact]
    public async Task Charge_AuthorizeAndCapture_IsPaid()
    {
        Begin();
        RequireCapability(Capabilities.SupportsAuthorizeAndCapture, "authorize-and-capture charges");
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);

        var result = await ChargeAsync(CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, StoredPaymentCaptureMode.AuthorizeAndCapture));

        AssertSucceeded(result, scenario.Amount);
        result.NewPaymentStatus.Should().Be(PaymentStatus.Paid, "AuthorizeAndCapture captures in the same call");
    }

    [Theory]
    [InlineData(MerchantInitiatedReason.Recurring)]
    [InlineData(MerchantInitiatedReason.Unscheduled)]
    [InlineData(MerchantInitiatedReason.Resubmission)]
    [InlineData(MerchantInitiatedReason.Reauthorization)]
    [InlineData(MerchantInitiatedReason.DelayedCharge)]
    public async Task Charge_EveryMerchantInitiatedReason_IsAccepted(MerchantInitiatedReason reason)
    {
        Begin();
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);

        var request = CreateChargeRequest(stored, scenario.Amount, NewKey(), reason, DefaultCaptureMode);
        request.Usage = reason == MerchantInitiatedReason.Unscheduled ? StoredCredentialUsage.Unscheduled : StoredCredentialUsage.Recurring;
        var result = await ChargeAsync(request);

        AssertSucceeded(result, scenario.Amount);
    }

    [Fact]
    public async Task Charge_SameIdempotencyKey_NeverChargesTwice()
    {
        Begin();
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);
        var key = NewKey();

        var first = await ChargeAsync(CreateChargeRequest(stored, scenario.Amount, key, MerchantInitiatedReason.Recurring, DefaultCaptureMode));
        var replay = await ChargeAsync(CreateChargeRequest(stored, scenario.Amount, key, MerchantInitiatedReason.Recurring, DefaultCaptureMode));

        first.Outcome.Should().Be(ChargeOutcome.Succeeded, "error: {0} ({1})", first.RawMessage, first.RawCode);
        replay.Outcome.Should().Be(first.Outcome, "a replay returns the first result");
        replay.TransactionId.Should().Be(first.TransactionId, "a replay with the same idempotency key must not create a second charge");
        replay.Amount.Should().Be(first.Amount);
        replay.NewPaymentStatus.Should().Be(first.NewPaymentStatus);
    }

    [Fact]
    public async Task Charge_DifferentIdempotencyKeys_ChargeIndependently()
    {
        Begin();
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);

        var first = await ChargeAsync(CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode));
        var second = await ChargeAsync(CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode));

        first.Outcome.Should().Be(ChargeOutcome.Succeeded);
        second.Outcome.Should().Be(ChargeOutcome.Succeeded);
        second.TransactionId.Should().NotBe(first.TransactionId, "each renewal is its own charge");
    }

    [Fact]
    public async Task Charge_SameIdempotencyKey_ReplaysDecline()
    {
        Begin();
        var scenario = Require(Scenarios.SoftDecline, "a temporary decline on a merchant-initiated charge");
        var first = await ChargeScenarioAsync(scenario);
        var replay = await ChargeAsync(CreateChargeRequest(first.Stored, scenario.Amount, first.Request.IdempotencyKey, MerchantInitiatedReason.Recurring, DefaultCaptureMode));

        first.Result.Outcome.Should().Be(ChargeOutcome.SoftDecline);
        replay.Outcome.Should().Be(ChargeOutcome.SoftDecline, "a replay with the same idempotency key returns the first result, declines included");
        replay.RawCode.Should().Be(first.Result.RawCode);
    }

    [Fact]
    public async Task Charge_SoftDecline_IsClassifiedWithCodesAndRetryTiming()
    {
        Begin();
        var scenario = Require(Scenarios.SoftDecline, "a temporary decline on a merchant-initiated charge");

        var charge = await ChargeScenarioAsync(scenario);
        var result = charge.Result;

        AssertDeclined(result, ChargeOutcome.SoftDecline, scenario);
        if (scenario.ExpectRetryAfter)
        {
            result.RetryAfter.Should().NotBeNull("the provider gives retry timing for {0}, so RetryAfter must carry it", scenario);
        }

        if (result.RetryAfter != null)
        {
            result.RetryAfter.Value.Should().BePositive("RetryAfter is a delay from now");
        }
    }

    [Fact]
    public async Task Charge_HardDecline_IsClassifiedWithRawCodeAndNoRetry()
    {
        Begin();
        var scenario = Require(Scenarios.HardDecline, "a permanent decline on a merchant-initiated charge");

        var charge = await ChargeScenarioAsync(scenario);

        AssertDeclined(charge.Result, ChargeOutcome.HardDecline, scenario);
        charge.Result.RetryAfter.Should().BeNull("a permanent decline is never retried");
    }

    [Fact]
    public async Task Charge_HardDeclineWithAdviceCode_KeepsAdviceCode()
    {
        Begin();
        var scenario = Require(Scenarios.HardDeclineWithAdviceCode, "a permanent decline with a network advice code");

        var charge = await ChargeScenarioAsync(scenario);

        AssertDeclined(charge.Result, ChargeOutcome.HardDecline, scenario);
        charge.Result.AdviceCode.Should().NotBeNullOrWhiteSpace("the network advice code (e.g. Mastercard MAC) decides whether a retry is allowed");
        charge.Result.RetryAfter.Should().BeNull("a permanent decline is never retried");
    }

    [Fact]
    public async Task Charge_RequiresAction_IsClassifiedAndNotReportedAsDecline()
    {
        Begin();
        var scenario = Require(Scenarios.RequiresActionOnMerchantInitiatedCharge, "a merchant-initiated charge that requires authentication");

        var charge = await ChargeScenarioAsync(scenario);
        var result = charge.Result;

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.RequiresAction, "{0} must ask the customer to authenticate, not be retried or treated as a decline", scenario);
        result.NewPaymentStatus.Should().NotBe(PaymentStatus.Authorized).And.NotBe(PaymentStatus.Paid);
    }

    [Fact]
    public async Task Charge_RequiresAction_GivesStorefrontARecoveryPath()
    {
        Begin();
        var scenario = Require(Scenarios.RequiresActionOnMerchantInitiatedCharge, "a merchant-initiated charge that requires authentication");

        var charge = await ChargeScenarioAsync(scenario);

        charge.Result.Outcome.Should().Be(ChargeOutcome.RequiresAction);
        AssertRecoveryPath(charge.Result);
    }

    [Fact]
    public async Task Charge_ProviderError_IsClassifiedAndRetryableWithSameKey()
    {
        Begin();
        var scenario = Require(Scenarios.ProviderError, "a gateway or processor error");

        var charge = await ChargeScenarioAsync(scenario);
        var result = charge.Result;

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ChargeOutcome.ProviderError, "{0} is a gateway error, not a decline", scenario);
        result.NewPaymentStatus.Should().NotBe(PaymentStatus.Authorized).And.NotBe(PaymentStatus.Paid);

        if (!scenario.RetryWithSameKeySucceeds)
        {
            return;
        }

        // The platform retries a provider error with the same idempotency key; an error must not be replayed as the final result.
        var retry = await ChargeAsync(CreateChargeRequest(charge.Stored, scenario.Amount, charge.Request.IdempotencyKey, MerchantInitiatedReason.Recurring, DefaultCaptureMode));
        retry.Outcome.Should().Be(ChargeOutcome.Succeeded, "a retry with the same idempotency key after a transient error must go through; error: {0} ({1})", retry.RawMessage, retry.RawCode);
        retry.TransactionId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Charge_WithoutStoredPaymentMethod_NeverSucceeds()
    {
        Begin();
        var scenario = RequireSuccess();
        var request = CreateChargeRequest(null, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode);

        await AssertNeverSucceedsAsync(request, "there is no stored payment method to charge");
    }

    [Fact]
    public async Task Charge_StoredMethodWithoutToken_NeverSucceeds()
    {
        Begin();
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);
        var tokenless = (StoredPaymentMethod)stored.Clone();
        tokenless.Token = null;

        await AssertNeverSucceedsAsync(CreateChargeRequest(tokenless, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode),
            "the stored payment method has no provider token");
    }

    // Authorize, then capture when the order ships

    [Fact]
    public async Task AuthorizeOnly_ThenCapture_IsPaid()
    {
        Begin();
        RequireCapability(Capabilities.SupportsAuthorizeOnly && Capabilities.SupportsCapture, "authorize-only charges followed by capture");
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);
        var request = CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, StoredPaymentCaptureMode.AuthorizeOnly);
        var authorization = await ChargeAsync(request);
        AssertSucceeded(authorization, scenario.Amount);
        authorization.NewPaymentStatus.Should().Be(PaymentStatus.Authorized);

        var capture = await Connector.CaptureProcessPaymentAsync(Scenarios.CreateCaptureRequest(request, authorization), CancellationToken);

        capture.Should().NotBeNull();
        capture.IsSuccess.Should().BeTrue("the authorization must be capturable; error: {0}", capture.ErrorMessage);
        capture.NewPaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    // Deleting a stored method

    [Fact]
    public async Task Delete_StoredMethod_Succeeds()
    {
        Begin();
        RequireCapability(Capabilities.SupportsDelete, "deleting stored methods");
        var stored = await StoreSuccessfullyAsync(RequireSuccessCard());

        var result = await DeleteAsync(stored);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("error: {0}", result.ErrorMessage);
    }

    [Fact]
    public async Task Delete_StoredMethod_TokenCanNoLongerBeCharged()
    {
        Begin();
        RequireCapability(Capabilities.SupportsDelete, "deleting stored methods");
        RequireCapability(Capabilities.RejectsDeletedToken, "rejecting charges against a deleted token in the sandbox");
        var scenario = RequireSuccess();
        var stored = await StoreSuccessfullyAsync(scenario.Card);
        (await DeleteAsync(stored)).IsSuccess.Should().BeTrue();

        var result = await ChargeAsync(CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode));

        result.IsSuccess.Should().BeFalse("a deleted token must not be charged");
        result.Outcome.Should().Be(ChargeOutcome.HardDecline, "the customer needs a new payment method; retrying can't help");
    }

    // Unsupported features

    [Fact]
    public async Task UnsupportedFeatures_FailExplicitlyAndConsistently()
    {
        Begin();
        var behaviours = new Dictionary<string, string>();

        if (!Capabilities.SupportsAuthorizeOnly || !Capabilities.SupportsAuthorizeAndCapture)
        {
            var scenario = RequireSuccess();
            var stored = await StoreSuccessfullyAsync(scenario.Card);
            if (!Capabilities.SupportsAuthorizeOnly)
            {
                behaviours["AuthorizeOnly"] = await ObserveAsync(() => ChargeAsync(CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, StoredPaymentCaptureMode.AuthorizeOnly)));
            }

            if (!Capabilities.SupportsAuthorizeAndCapture)
            {
                behaviours["AuthorizeAndCapture"] = await ObserveAsync(() => ChargeAsync(CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, StoredPaymentCaptureMode.AuthorizeAndCapture)));
            }
        }

        if (!Capabilities.SupportsDelete)
        {
            var stored = await StoreSuccessfullyAsync(RequireSuccessCard());
            behaviours["Delete"] = await ObserveAsync(() => DeleteAsync(stored));
        }

        if (behaviours.Count == 0)
        {
            Assert.Skip("The connector declares every optional feature as supported.");
        }

        behaviours.Should().NotContain(x => x.Value == Behaviour.Succeeded, "an unsupported feature must not report success");
        behaviours.Should().NotContain(x => x.Value != Behaviour.UnsuccessfulResult && x.Value != Behaviour.NotSupported,
            "an unsupported feature returns an unsuccessful result or throws NotSupportedException, never NotImplementedException or another error");
        behaviours.Values.Distinct().Should().HaveCount(1, "unsupported features fail the same way: {0}", string.Join(", ", behaviours.Select(x => $"{x.Key}={x.Value}")));
    }

    // Requests (override to add what the connector's provider needs, e.g. Order/Payment objects or invoice numbers)

    protected virtual StorePaymentMethodRequest CreateStoreRequest(CardScenario card)
    {
        return new StorePaymentMethodRequest
        {
            StoreId = Scenarios.StoreId,
            CustomerId = $"contract-customer-{Guid.NewGuid():N}",
            ConsentId = $"contract-consent-{Guid.NewGuid():N}",
            Usage = StoredCredentialUsage.Recurring,
            Verify = true,
            Parameters = Copy(card.StoreParameters),
        };
    }

    protected virtual ProcessStoredPaymentRequest CreateChargeRequest(StoredPaymentMethod storedMethod, decimal amount, string idempotencyKey, MerchantInitiatedReason reason, StoredPaymentCaptureMode captureMode)
    {
        return new ProcessStoredPaymentRequest
        {
            StoreId = Scenarios.StoreId,
            OrderId = $"contract-order-{Guid.NewGuid():N}",
            PaymentId = $"contract-payment-{Guid.NewGuid():N}",
            StoredPaymentMethod = storedMethod,
            Amount = amount,
            Currency = Scenarios.Currency,
            IdempotencyKey = idempotencyKey,
            Usage = StoredCredentialUsage.Recurring,
            Sequence = StoredCredentialSequence.Subsequent,
            Reason = reason,
            CaptureMode = captureMode,
        };
    }

    protected virtual string NewKey() => $"contract-{Guid.NewGuid():N}";

    protected StoredPaymentCaptureMode DefaultCaptureMode => Capabilities.SupportsAuthorizeOnly ? StoredPaymentCaptureMode.AuthorizeOnly : StoredPaymentCaptureMode.AuthorizeAndCapture;

    // Operations

    protected async Task<(StorePaymentMethodRequest Request, StorePaymentMethodRequestResult Result)> StoreAsync(CardScenario card, Action<StorePaymentMethodRequest> configure = null)
    {
        var request = CreateStoreRequest(card);
        configure?.Invoke(request);

        var result = await Connector.StorePaymentMethodAsync(request, CancellationToken);
        result.Should().NotBeNull("StorePaymentMethodAsync must return a result");
        if (result.StoredPaymentMethod != null)
        {
            _storedMethods.Add(result.StoredPaymentMethod);
        }

        return (request, result);
    }

    protected async Task<StoredPaymentMethod> StoreSuccessfullyAsync(CardScenario card)
    {
        var (_, result) = await StoreAsync(card);
        result.IsSuccess.Should().BeTrue("{0} must be saved before it can be charged; error: {1} ({2})", card, result.ErrorMessage, result.RawCode);
        result.StoredPaymentMethod.Should().NotBeNull();
        return result.StoredPaymentMethod;
    }

    protected async Task<ProcessStoredPaymentRequestResult> ChargeAsync(ProcessStoredPaymentRequest request)
    {
        var result = await Connector.ProcessStoredPaymentAsync(request, CancellationToken);
        result.Should().NotBeNull("ProcessStoredPaymentAsync must return a result");
        return result;
    }

    protected async Task<ChargeAttempt> ChargeScenarioAsync(ChargeScenario scenario)
    {
        scenario.Card.Should().NotBeNull("scenario {0} needs a card", scenario);
        var stored = await StoreSuccessfullyAsync(scenario.Card);
        scenario.AfterStore?.Invoke(stored);

        var request = CreateChargeRequest(stored, scenario.Amount, NewKey(), MerchantInitiatedReason.Recurring, DefaultCaptureMode);
        var result = await ChargeAsync(request);
        return new ChargeAttempt(stored, request, result);
    }

    protected async Task<DeleteStoredPaymentMethodRequestResult> DeleteAsync(StoredPaymentMethod stored)
    {
        var result = await Connector.DeleteStoredPaymentMethodAsync(new DeleteStoredPaymentMethodRequest { StoreId = Scenarios.StoreId, StoredPaymentMethod = stored }, CancellationToken);
        _deletedMethods.Add(stored);
        return result;
    }

    // Removes what the tests stored, so sandbox accounts don't fill up with test profiles.
    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_connector == null || !Capabilities.SupportsDelete)
        {
            return;
        }

        foreach (var method in _storedMethods.Where(x => !_deletedMethods.Contains(x)))
        {
            try
            {
                await _connector.DeleteStoredPaymentMethodAsync(new DeleteStoredPaymentMethodRequest { StoreId = Scenarios.StoreId, StoredPaymentMethod = method }, CancellationToken.None);
            }
            catch (Exception)
            {
                // Best effort: cleanup must not hide the test result.
            }
        }
    }

    // Skips

    /// <summary>Skips the calling test when the scenarios skip every test or list this one as a known issue.</summary>
    protected void Begin([CallerMemberName] string testName = "")
    {
        if (Scenarios.SkipReason != null)
        {
            Assert.Skip(Scenarios.SkipReason);
        }

        var knownIssues = Scenarios.KnownIssues;
        if (knownIssues != null && knownIssues.TryGetValue(testName, out var reason))
        {
            Assert.Skip(reason);
        }
    }

    protected static T Require<T>(T scenario, string what)
        where T : class
    {
        if (scenario == null)
        {
            Assert.Skip($"The sandbox can't simulate {what}.");
        }

        return scenario;
    }

    protected static void RequireCapability(bool supported, string what)
    {
        if (!supported)
        {
            Assert.Skip($"The connector doesn't support {what}.");
        }
    }

    private ChargeScenario RequireSuccess()
    {
        var scenario = Scenarios.Success ?? throw new InvalidOperationException("IConnectorTestScenarios.Success is required.");
        scenario.Card.Should().NotBeNull("the Success scenario needs a card");
        return scenario;
    }

    private CardScenario RequireSuccessCard() => RequireSuccess().Card;

    // Assertions

    private static void AssertSucceeded(ProcessStoredPaymentRequestResult result, decimal amount)
    {
        result.Outcome.Should().Be(ChargeOutcome.Succeeded, "error: {0} ({1})", result.RawMessage, result.RawCode);
        result.IsSuccess.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrWhiteSpace("the transaction id is kept on the payment for capture and refunds");
        if (result.Amount != null)
        {
            result.Amount.Should().Be(amount, "the charged amount is the requested amount");
        }
    }

    private static void AssertDeclined(ProcessStoredPaymentRequestResult result, ChargeOutcome expected, ChargeScenario scenario)
    {
        result.IsSuccess.Should().BeFalse("{0} must be declined", scenario);
        result.Outcome.Should().Be(expected, "{0} must be classified as {1}; raw code {2}", scenario, expected, result.RawCode);
        result.NewPaymentStatus.Should().NotBe(PaymentStatus.Authorized).And.NotBe(PaymentStatus.Paid);
        result.RawCode.Should().NotBeNullOrWhiteSpace("declines keep the provider code for support and dispute handling");

        if (scenario.ExpectedRawCode != null)
        {
            result.RawCode.Should().Be(scenario.ExpectedRawCode);
        }

        if (scenario.ExpectedAdviceCode != null)
        {
            result.AdviceCode.Should().Be(scenario.ExpectedAdviceCode, "the network advice code decides whether and when a retry is allowed");
        }
    }

    private void AssertRecoveryPath(StoredCredentialResultBase result)
    {
        var keys = Scenarios.RequiresActionPublicParameters ?? [];
        var hasRecoveryUrl = !string.IsNullOrWhiteSpace(result.RecoveryUrl);
        var hasParameters = keys.Count > 0 && keys.All(key => result.PublicParameters != null && result.PublicParameters.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value));

        (hasRecoveryUrl || hasParameters).Should().BeTrue(
            "a RequiresAction result must give the storefront something to act on: RecoveryUrl, or the documented public parameters [{0}]; got RecoveryUrl={1}, PublicParameters=[{2}]",
            string.Join(", ", keys),
            result.RecoveryUrl ?? "<null>",
            result.PublicParameters == null ? string.Empty : string.Join(", ", result.PublicParameters.Keys));
    }

    private async Task AssertNeverSucceedsAsync(ProcessStoredPaymentRequest request, string because)
    {
        ProcessStoredPaymentRequestResult result;
        try
        {
            result = await Connector.ProcessStoredPaymentAsync(request, CancellationToken);
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("the charge must fail: {0}", because);
        result.Outcome.Should().NotBe(ChargeOutcome.Succeeded, because);
    }

    // Helpers

    /// <summary>A saved card and the merchant-initiated charge made against it.</summary>
    protected sealed record ChargeAttempt(StoredPaymentMethod Stored, ProcessStoredPaymentRequest Request, ProcessStoredPaymentRequestResult Result);

    private static class Behaviour
    {
        public const string Succeeded = "Succeeded";
        public const string UnsuccessfulResult = "UnsuccessfulResult";
        public const string NotSupported = "NotSupportedException";
    }

    private static async Task<string> ObserveAsync<T>(Func<Task<T>> action)
        where T : PaymentRequestResultBase
    {
        try
        {
            var result = await action();
            return result?.IsSuccess == true ? Behaviour.Succeeded : Behaviour.UnsuccessfulResult;
        }
        catch (NotSupportedException)
        {
            return Behaviour.NotSupported;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name;
        }
    }

    private static bool Overrides(Type type, string methodName, Type requestType)
    {
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, [requestType, typeof(CancellationToken)]);
        return method != null && method.DeclaringType != typeof(PaymentMethod);
    }

    private static NameValueCollection Copy(NameValueCollection source)
    {
        return source == null ? new NameValueCollection() : new NameValueCollection(source);
    }

    private static IEnumerable<string> StringValues(StoredPaymentMethod method)
    {
        return method.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.PropertyType == typeof(string) && x.CanRead && x.GetIndexParameters().Length == 0)
            .Select(x => x.GetValue(method) as string)
            .Where(x => !string.IsNullOrEmpty(x));
    }
}
