namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>The bank asked for authentication (3-D Secure): link to the order payment page.</summary>
public class RenewalPaymentActionRequiredEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalPaymentActionRequiredEmailNotification))
{
    public string PaymentUrl { get; set; }
}
