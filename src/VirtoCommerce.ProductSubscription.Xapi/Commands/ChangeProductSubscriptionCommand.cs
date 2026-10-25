using System;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class ChangeProductSubscriptionCommand : SubscriptionIdCommand
{
    public string IntervalUnit { get; set; }
    public int? IntervalCount { get; set; }
    public int? Quantity { get; set; }
    public DateTime? NextOrderDate { get; set; }
    public string SavedPaymentMethodId { get; set; }
}
