namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>A paused subscription is active again, by the customer or on its resume date.</summary>
public class SubscriptionResumedEmailNotification() : SubscriptionEmailNotificationBase(nameof(SubscriptionResumedEmailNotification))
{
    /// <summary>True when the subscription resumed by itself on the date set when it was paused.</summary>
    public bool IsAutomatic { get; set; }
}
