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
