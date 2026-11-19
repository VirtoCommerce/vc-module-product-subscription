namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>A subscription was paused: no deliveries and no charges until it is resumed.</summary>
public class SubscriptionPausedEmailNotification() : SubscriptionEmailNotificationBase(nameof(SubscriptionPausedEmailNotification))
{
    /// <summary>The date the subscription resumes by itself; empty when it stays paused until the customer resumes it.</summary>
    public string PausedUntilText { get; set; }
}
