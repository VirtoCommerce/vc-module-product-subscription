using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Model.Requests;

namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

/// <summary>
/// Supplies the inputs that make a connector's sandbox produce each outcome the contract tests check.
/// Every scenario except <see cref="Success"/> is optional: return null when the sandbox can't simulate it and the
/// tests that need it are reported as skipped. Derive from <see cref="ConnectorTestScenarios"/> to get the defaults.
/// </summary>
public interface IConnectorTestScenarios
{
    /// <summary>
    /// When not null, every contract test is skipped with this reason, e.g. "Set AUTHORIZENET_API_LOGIN_ID to run the
    /// sandbox tests" when the sandbox credentials aren't in the environment.
    /// </summary>
    string SkipReason { get; }

    /// <summary>Store id put on every request.</summary>
    string StoreId { get; }

    /// <summary>Currency of every charge, e.g. "USD".</summary>
    string Currency { get; }

    /// <summary>Required. A card that saves and charges successfully.</summary>
    ChargeScenario Success { get; }

    /// <summary>A saved card whose merchant-initiated charge gets a temporary decline (e.g. insufficient funds).</summary>
    ChargeScenario SoftDecline { get; }

    /// <summary>A saved card whose merchant-initiated charge gets a permanent decline (e.g. expired, lost, closed).</summary>
    ChargeScenario HardDecline { get; }

    /// <summary>A permanent decline that carries a network advice code (e.g. Mastercard MAC 21, recurring payments cancelled).</summary>
    ChargeScenario HardDeclineWithAdviceCode { get; }

    /// <summary>A saved card whose merchant-initiated charge requires the customer to authenticate (3-D Secure).</summary>
    ChargeScenario RequiresActionOnMerchantInitiatedCharge { get; }

    /// <summary>A card that requires 3-D Secure to be saved, and how to complete it.</summary>
    AuthenticationScenario ThreeDsOnStore { get; }

    /// <summary>A card the provider refuses to save (the verification authorization is declined).</summary>
    CardScenario DeclinedOnStore { get; }

    /// <summary>A charge that triggers a gateway/processor error, if the sandbox supports one.</summary>
    ProviderErrorScenario ProviderError { get; }

    /// <summary>Which optional features the connector supports.</summary>
    ConnectorCapabilities Capabilities { get; }

    /// <summary>
    /// The documented keys of <see cref="PaymentRequestResultBase.PublicParameters"/> a <c>RequiresAction</c> result carries
    /// for the storefront (e.g. a client secret or challenge URL) when the connector doesn't set
    /// <see cref="StoredCredentialResultBase.RecoveryUrl"/>. Empty when the connector always sets RecoveryUrl.
    /// </summary>
    IReadOnlyCollection<string> RequiresActionPublicParameters { get; }

    /// <summary>
    /// Tests this connector is known to fail, by test method name, with the reason. They are reported as skipped with
    /// that reason, so a known gap stays visible without breaking the build. Start each reason with "BUG:" for real defects.
    /// </summary>
    IReadOnlyDictionary<string, string> KnownIssues { get; }

    /// <summary>
    /// Returns the original network transaction id the connector referenced on the wire for a merchant-initiated charge,
    /// e.g. read from a recording <c>HttpMessageHandler</c> or the provider's transaction-details API.
    /// Return null when it can't be observed; the test that needs it is then skipped.
    /// </summary>
    Task<string> GetReferencedNetworkTransactionIdAsync(ProcessStoredPaymentRequest request, ProcessStoredPaymentRequestResult result, CancellationToken cancellationToken);

    /// <summary>
    /// Builds the capture request for an authorized merchant-initiated charge, the way the Orders module would
    /// (connectors usually read <c>Payment</c> as a <c>PaymentIn</c> and use its <c>OuterId</c>).
    /// </summary>
    CapturePaymentRequest CreateCaptureRequest(ProcessStoredPaymentRequest authorizationRequest, ProcessStoredPaymentRequestResult authorization);
}
