using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Model.Requests;

namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

/// <summary>
/// Base for a connector's scenarios: every optional scenario is null (skipped) until overridden.
/// </summary>
public abstract class ConnectorTestScenarios : IConnectorTestScenarios
{
    public virtual string SkipReason => null;

    public virtual string StoreId => "contract-test-store";

    public virtual string Currency => "USD";

    public abstract ChargeScenario Success { get; }

    public virtual ChargeScenario SoftDecline => null;

    public virtual ChargeScenario HardDecline => null;

    public virtual ChargeScenario HardDeclineWithAdviceCode => null;

    public virtual ChargeScenario RequiresActionOnMerchantInitiatedCharge => null;

    public virtual AuthenticationScenario ThreeDsOnStore => null;

    public virtual CardScenario DeclinedOnStore => null;

    public virtual ProviderErrorScenario ProviderError => null;

    public virtual ConnectorCapabilities Capabilities { get; } = new();

    public virtual IReadOnlyCollection<string> RequiresActionPublicParameters => [];

    public virtual IReadOnlyDictionary<string, string> KnownIssues => new Dictionary<string, string>();

    public virtual Task<string> GetReferencedNetworkTransactionIdAsync(ProcessStoredPaymentRequest request, ProcessStoredPaymentRequestResult result, CancellationToken cancellationToken)
    {
        return Task.FromResult<string>(null);
    }

    public virtual CapturePaymentRequest CreateCaptureRequest(ProcessStoredPaymentRequest authorizationRequest, ProcessStoredPaymentRequestResult authorization)
    {
        return new CapturePaymentRequest
        {
            StoreId = authorizationRequest.StoreId,
            Store = authorizationRequest.Store,
            OrderId = authorizationRequest.OrderId,
            Order = authorizationRequest.Order,
            PaymentId = authorizationRequest.PaymentId,
            Payment = authorizationRequest.Payment,
            OuterId = authorization.TransactionId,
            CaptureAmount = authorization.Amount ?? authorizationRequest.Amount,
        };
    }
}
