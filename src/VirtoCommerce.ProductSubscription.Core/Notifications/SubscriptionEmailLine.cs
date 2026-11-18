namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>One product of a subscription email, with every text formatted for the email's language and currency.</summary>
public class SubscriptionEmailLine
{
    public string SubscriptionId { get; set; }

    /// <summary>The subscription ID the customer sees on the storefront, e.g. "SUB-000209".</summary>
    public string SubscriptionNumber { get; set; }

    public string ProductName { get; set; }
    public int Quantity { get; set; }

    /// <summary>"Every month", "Every 2 weeks".</summary>
    public string FrequencyText { get; set; }

    /// <summary>"Save 10%"; empty without a subscription discount.</summary>
    public string DiscountText { get; set; }

    /// <summary>The line total of a delivery; empty in emails about subscriptions rather than a delivery.</summary>
    public string TotalText { get; set; }

    /// <summary>The next delivery date of the subscription.</summary>
    public string NextDateText { get; set; }

    /// <summary>Link to this subscription on the customer's subscriptions page.</summary>
    public string ManageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    /// <summary>Why the item can't be delivered as it is (out of stock, quantity limits).</summary>
    public string Message { get; set; }
}
