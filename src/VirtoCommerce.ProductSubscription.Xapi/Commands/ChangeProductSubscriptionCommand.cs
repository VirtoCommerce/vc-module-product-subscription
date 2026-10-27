using System;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class ChangeProductSubscriptionCommand : SubscriptionIdCommand
{
    public SubscriptionIntervalUnit? IntervalUnit { get; set; }
    public int? IntervalCount { get; set; }
    public int? Quantity { get; set; }
    public DateTime? NextOrderDate { get; set; }
    public string SavedPaymentMethodId { get; set; }
}
