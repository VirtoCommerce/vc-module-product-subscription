using System;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SkipProductSubscriptionCommand : SubscriptionIdCommand
{
    /// <summary>The delivery the customer saw; a stale one makes the skip fail instead of skipping a later delivery.</summary>
    public DateTime? ExpectedNextOrderDate { get; set; }
}
