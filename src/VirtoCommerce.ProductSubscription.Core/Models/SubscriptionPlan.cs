using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// What can be subscribed to: the frequencies offered to customers, the subscription discount,
/// and the products and categories the plan applies to.
/// </summary>
public class SubscriptionPlan : AuditableEntity, ICloneable
{
    public string StoreId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>When several plans match a product, the plan with the highest priority wins.</summary>
    public int Priority { get; set; }

    public SubscriptionDiscountType DiscountType { get; set; } = SubscriptionDiscountType.Percent;
    public decimal DiscountValue { get; set; }

    public IList<SubscriptionPlanInterval> Intervals { get; set; } = new List<SubscriptionPlanInterval>();

    public IList<string> ProductIds { get; set; } = new List<string>();
    public IList<string> CategoryIds { get; set; } = new List<string>();

    public virtual SubscriptionPlanInterval GetDefaultInterval()
    {
        return Intervals?.FirstOrDefault(x => x.IsDefault) ?? Intervals?.FirstOrDefault();
    }

    public virtual bool SupportsInterval(SubscriptionIntervalUnit unit, int count)
    {
        return Intervals?.Any(x => x.Unit == unit && x.Count == count) == true;
    }

    public virtual object Clone()
    {
        var result = (SubscriptionPlan)MemberwiseClone();
        result.Intervals = Intervals?.Select(x => (SubscriptionPlanInterval)x.Clone()).ToList();
        result.ProductIds = ProductIds?.ToList();
        result.CategoryIds = CategoryIds?.ToList();
        return result;
    }
}

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

public enum SubscriptionIntervalUnit
{
    Day,
    Week,
    Month,
    Year,
}

public enum SubscriptionDiscountType
{
    None,
    Percent,
    FixedAmount,
}
