namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// The subscription actions a store lets its customers take from the storefront. The back office is not limited by them.
/// </summary>
public class SubscriptionCustomerActions
{
    public bool CanSkip { get; set; }
    public bool CanOrderNow { get; set; }
    public bool CanChangePaymentMethod { get; set; }
    public bool CanPause { get; set; }
    public bool CanChangeFrequency { get; set; }
}
