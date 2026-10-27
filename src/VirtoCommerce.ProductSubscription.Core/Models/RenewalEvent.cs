using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class RenewalEvent : Entity, ICloneable
{
    public DateTime EventDate { get; set; }
    public string State { get; set; }
    public string EventType { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
