namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// Subscription terms the customer agrees to at checkout (US auto-renewal disclosure).
/// </summary>
public class SubscriptionTerms
{
    public string Version { get; set; }
    public string Text { get; set; }
    public string Hash { get; set; }
}
