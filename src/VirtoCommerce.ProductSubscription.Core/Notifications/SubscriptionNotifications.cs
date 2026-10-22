using System.Collections.Generic;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Notifications;

public abstract class SubscriptionEmailNotificationBase(string type) : EmailNotification(type)
{
    public string CustomerName { get; set; }
    public string StoreName { get; set; }
    public string StoreUrl { get; set; }

    /// <summary>Link to the customer's subscriptions page.</summary>
    public string ManageUrl { get; set; }

    public IList<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public Renewal Renewal { get; set; }
}

/// <summary>Acknowledgment sent right after sign-up: terms, schedule, how to cancel.</summary>
public class SubscriptionCreatedEmailNotification() : SubscriptionEmailNotificationBase(nameof(SubscriptionCreatedEmailNotification));

/// <summary>Sent before each renewal: items, maximum price, cutoff date, links to change or skip.</summary>
public class RenewalReminderEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalReminderEmailNotification));

/// <summary>Price above the reminder amount or an item is unavailable: the customer must review.</summary>
public class RenewalReviewRequiredEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalReviewRequiredEmailNotification));

/// <summary>The bank asked for authentication (3-D Secure): link to the order payment page.</summary>
public class RenewalPaymentActionRequiredEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalPaymentActionRequiredEmailNotification))
{
    public string PaymentUrl { get; set; }
}

/// <summary>The renewal could not be charged: retry information or a request to update the card.</summary>
public class RenewalPaymentFailedEmailNotification() : SubscriptionEmailNotificationBase(nameof(RenewalPaymentFailedEmailNotification))
{
    public bool WillRetry { get; set; }
}

/// <summary>Confirmation that a subscription was cancelled.</summary>
public class SubscriptionCancelledEmailNotification() : SubscriptionEmailNotificationBase(nameof(SubscriptionCancelledEmailNotification));

/// <summary>Yearly reminder of every active subscription (California, Minnesota, Connecticut).</summary>
public class SubscriptionAnnualReminderEmailNotification() : SubscriptionEmailNotificationBase(nameof(SubscriptionAnnualReminderEmailNotification));
