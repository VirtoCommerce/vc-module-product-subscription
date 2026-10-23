using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;

namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

/// <summary>
/// A card (or provider token) the contract tests save with <c>StorePaymentMethodAsync</c>.
/// </summary>
public class CardScenario
{
    /// <summary>Shown in assertion messages, e.g. "Visa 4111 (approved)".</summary>
    public string Description { get; set; }

    /// <summary>
    /// What the storefront would post for this card: hosted-field tokens, nonces, opaque data, test card fields.
    /// Becomes <see cref="PaymentRequestBase.Parameters"/> of the store request. Read secrets from environment variables.
    /// </summary>
    public NameValueCollection StoreParameters { get; set; } = new();

    /// <summary>Expected <see cref="StoredPaymentMethod.Last4"/>, or null to only check it is four digits.</summary>
    public string ExpectedLast4 { get; set; }

    /// <summary>Expected <see cref="StoredPaymentMethod.Brand"/> (case-insensitive), or null to only check it is set.</summary>
    public string ExpectedBrand { get; set; }

    /// <summary>
    /// The full card number, when the scenario knows it. The tests check it never appears in the stored payment method
    /// (Virto must never keep the PAN). Null skips that check.
    /// </summary>
    public string CardNumber { get; set; }

    public override string ToString() => Description ?? base.ToString();
}

/// <summary>
/// A merchant-initiated charge: the card is saved first, then charged with <c>ProcessStoredPaymentAsync</c>.
/// </summary>
public class ChargeScenario
{
    public string Description { get; set; }

    /// <summary>The card to save before charging it.</summary>
    public CardScenario Card { get; set; }

    /// <summary>Amount to charge. Many sandboxes trigger declines by amount rather than by card.</summary>
    public decimal Amount { get; set; } = 10m;

    /// <summary>
    /// Optional change applied to the saved method before charging, to simulate what happens between storage and
    /// renewal (e.g. the card expires or the cardholder cancels recurring payments).
    /// </summary>
    public Action<StoredPaymentMethod> AfterStore { get; set; }

    /// <summary>Expected <see cref="StoredCredentialResultBase.RawCode"/>, or null to only check a decline has one.</summary>
    public string ExpectedRawCode { get; set; }

    /// <summary>Expected <see cref="StoredCredentialResultBase.AdviceCode"/> (e.g. Mastercard MAC "21"), or null to skip the check.</summary>
    public string ExpectedAdviceCode { get; set; }

    /// <summary>True when the provider returns retry timing for this decline, so <see cref="StoredCredentialResultBase.RetryAfter"/> must be set.</summary>
    public bool ExpectRetryAfter { get; set; }

    public override string ToString() => Description ?? base.ToString();
}

/// <summary>
/// A card whose storage requires customer authentication (3-D Secure) before it can be saved.
/// </summary>
public class AuthenticationScenario
{
    public string Description { get; set; }

    /// <summary>The card; the first store request must come back with <see cref="ChargeOutcome.RequiresAction"/>.</summary>
    public CardScenario Card { get; set; }

    /// <summary>
    /// Completes authentication the way the storefront would (e.g. runs the sandbox's frictionless challenge, or posts
    /// the challenge result) and returns the parameters of the second store request.
    /// Receives the first request and its <see cref="ChargeOutcome.RequiresAction"/> result.
    /// </summary>
    public Func<StorePaymentMethodRequest, StorePaymentMethodRequestResult, CancellationToken, Task<NameValueCollection>> CompleteAuthenticationAsync { get; set; }

    public override string ToString() => Description ?? base.ToString();
}

/// <summary>
/// A charge the sandbox answers with a gateway or processor error (not a decline).
/// </summary>
public class ProviderErrorScenario : ChargeScenario
{
    /// <summary>
    /// True when the error is transient in the sandbox, so retrying with the same idempotency key must succeed.
    /// Leave false when the sandbox fails the same input every time.
    /// </summary>
    public bool RetryWithSameKeySucceeds { get; set; }
}

/// <summary>
/// Optional features of the stored credential flow. A feature the connector does not support must fail explicitly
/// (an unsuccessful result or <see cref="NotSupportedException"/>), never succeed silently or throw <see cref="NotImplementedException"/>.
/// </summary>
public class ConnectorCapabilities
{
    /// <summary><see cref="StoredPaymentCaptureMode.AuthorizeOnly"/>: authorize now, capture when the order ships (what renewals use).</summary>
    public bool SupportsAuthorizeOnly { get; set; } = true;

    /// <summary><see cref="StoredPaymentCaptureMode.AuthorizeAndCapture"/> in one call.</summary>
    public bool SupportsAuthorizeAndCapture { get; set; } = true;

    /// <summary>Capturing an authorized merchant-initiated charge with <c>CaptureProcessPaymentAsync</c>.</summary>
    public bool SupportsCapture { get; set; } = true;

    /// <summary>Deleting the stored method at the provider with <c>DeleteStoredPaymentMethodAsync</c>.</summary>
    public bool SupportsDelete { get; set; } = true;

    /// <summary>The sandbox rejects charges against a deleted token (some sandboxes don't enforce it).</summary>
    public bool RejectsDeletedToken { get; set; } = true;
}
