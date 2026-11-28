using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class SubscriptionPlanInterval : Entity, ICloneable
{
    public SubscriptionIntervalUnit Unit { get; set; }
    public int Count { get; set; } = 1;
    public bool IsDefault { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
