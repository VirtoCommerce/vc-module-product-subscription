namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

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
