using System;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class PauseProductSubscriptionCommand : SubscriptionIdCommand
{
    public DateTime? ResumeDate { get; set; }
}
