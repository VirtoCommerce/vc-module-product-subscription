using System;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;

namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

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
