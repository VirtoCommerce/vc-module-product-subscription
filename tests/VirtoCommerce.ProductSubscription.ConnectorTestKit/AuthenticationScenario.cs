using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;

namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

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
