using System.Collections.Generic;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>
/// The model every subscription email is rendered with. Besides the raw <see cref="Subscriptions"/> and
/// <see cref="Renewal"/>, it carries texts already formatted in the email's language and the store's currency
/// (the <c>*Text</c> properties and <see cref="Lines"/>), so the templates only place them.
/// </summary>
public abstract class SubscriptionEmailNotificationBase(string type) : EmailNotification(type)
{
    public string CustomerName { get; set; }

    /// <summary>The customer's first name for the greeting; empty when unknown, so the template greets without a name.</summary>
    public string FirstName { get; set; }

    public string StoreName { get; set; }
    public string StoreUrl { get; set; }

    /// <summary>The store's logo for the email header (store setting); empty shows the store name instead.</summary>
    public string LogoUrl { get; set; }

    /// <summary>The colour of buttons and links (store setting).</summary>
    public string AccentColor { get; set; }

    /// <summary>Link to the customer's subscriptions page, opened at the first subscription of this email.</summary>
    public string ManageUrl { get; set; }

    /// <summary>Link to the customer's subscriptions page without a subscription to open.</summary>
    public string SubscriptionsUrl { get; set; }

    /// <summary>Link to the delivery's order, when one was placed.</summary>
    public string OrderUrl { get; set; }

    /// <summary>The products of this email, ready to show: one line per renewal item (or per subscription).</summary>
    public IList<SubscriptionEmailLine> Lines { get; set; } = new List<SubscriptionEmailLine>();

    /// <summary>The delivery date, e.g. "November 20, 2026".</summary>
    public string DeliveryDateText { get; set; }

    /// <summary>The last day to change or skip the delivery, in the store's time zone.</summary>
    public string CutoffDateText { get; set; }

    /// <summary>The next delivery after this email's event (skip, resume), when known.</summary>
    public string NextDeliveryDateText { get; set; }

    /// <summary>The delivery total: the order total once ordered, otherwise the amount from the reminder.</summary>
    public string TotalText { get; set; }

    /// <summary>The card that pays, e.g. "Visa •••• 4242"; empty when there is no saved card.</summary>
    public string CardText { get; set; }

    public IList<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public Renewal Renewal { get; set; }
}
