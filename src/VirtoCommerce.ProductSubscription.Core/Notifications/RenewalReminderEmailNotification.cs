namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>Sent before each renewal: items, maximum price, cutoff date, links to change or skip.</summary>
public class RenewalReminderEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalReminderEmailNotification));
