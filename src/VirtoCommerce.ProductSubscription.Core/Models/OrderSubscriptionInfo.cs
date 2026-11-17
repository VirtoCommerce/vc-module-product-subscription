using System;
using System.Collections.Generic;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// How an order relates to subscriptions: the renewal that placed it on a delivery date (if any) and the subscriptions
/// whose products it holds, started by it or delivered by it. Built on the server, so a storefront doesn't have to
/// load every subscription and renewal of the customer to mark its orders.
/// </summary>
public class OrderSubscriptionInfo
{
    public string OrderId { get; set; }

    /// <summary>The renewal that placed this order; null for an order the customer placed.</summary>
    public string RenewalId { get; set; }

    public string RenewalNumber { get; set; }

    /// <summary>The delivery date of that renewal.</summary>
    public DateTime? RenewalScheduledDate { get; set; }

    /// <summary>Whether the renewal was ordered on request (Order now) rather than on its date.</summary>
    public bool IsManualRenewal { get; set; }

    /// <summary>The subscriptions this order started or delivered, one per order line product.</summary>
    public IList<OrderSubscriptionLine> Subscriptions { get; set; } = [];
}

/// <summary>A subscription behind one product of an order.</summary>
public class OrderSubscriptionLine
{
    public string SubscriptionId { get; set; }
    public string SubscriptionNumber { get; set; }
    public string ProductId { get; set; }
    public SubscriptionIntervalUnit IntervalUnit { get; set; }
    public int IntervalCount { get; set; }
    public string Status { get; set; }

    /// <summary>True when this order is the subscription's first one (the customer subscribed at checkout).</summary>
    public bool IsStartedByOrder { get; set; }
}
