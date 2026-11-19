using System;

namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>A delivery was skipped: nothing is charged for it, the next one stays as planned.</summary>
public class DeliverySkippedEmailNotification() : SubscriptionEmailNotificationBase(nameof(DeliverySkippedEmailNotification))
{
    /// <summary>True when the delivery was skipped because the customer didn't review the changes in time.</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>The skipped delivery date when no renewal was prepared for it yet (a skip ahead of the reminder).</summary>
    public DateTime? SkippedDate { get; set; }
}
