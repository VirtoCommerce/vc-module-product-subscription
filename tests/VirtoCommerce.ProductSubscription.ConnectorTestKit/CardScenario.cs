using System.Collections.Specialized;
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
