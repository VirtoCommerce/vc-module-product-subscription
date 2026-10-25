namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>The renewal could not be charged: retry information or a request to update the card.</summary>
public class RenewalPaymentFailedEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalPaymentFailedEmailNotification))
{
    public bool WillRetry { get; set; }
}
