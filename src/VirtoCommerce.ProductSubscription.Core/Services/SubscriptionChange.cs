using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

public class SubscriptionChange
{
    public SubscriptionIntervalUnit? IntervalUnit { get; set; }
    public int? IntervalCount { get; set; }
    public int? Quantity { get; set; }
    public System.DateTime? NextOrderDate { get; set; }
    public string StoredPaymentMethodId { get; set; }
}
