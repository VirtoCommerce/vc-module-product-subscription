namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>Price above the reminder amount or an item is unavailable: the customer must review.</summary>
public class RenewalReviewRequiredEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalReviewRequiredEmailNotification));
