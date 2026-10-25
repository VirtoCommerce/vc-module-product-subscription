using System;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class SubscriptionPlanIntervalEntity : Entity, IDataEntity<SubscriptionPlanIntervalEntity, SubscriptionPlanInterval>
{
    [Required]
    [StringLength(128)]
    public string PlanId { get; set; }
    public virtual SubscriptionPlanEntity Plan { get; set; }

    [Required]
    [StringLength(16)]
    public string Unit { get; set; }

    public int Count { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }

    public virtual SubscriptionPlanInterval ToModel(SubscriptionPlanInterval model)
    {
        model.Id = Id;
        model.Unit = EnumUtility.SafeParse(Unit, SubscriptionIntervalUnit.Month);
        model.Count = Count;
        model.IsDefault = IsDefault;
        return model;
    }

    public virtual SubscriptionPlanIntervalEntity FromModel(SubscriptionPlanInterval model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);
        Id = model.Id;
        Unit = model.Unit.ToString();
        Count = Math.Max(1, model.Count);
        IsDefault = model.IsDefault;
        return this;
    }

    public virtual void Patch(SubscriptionPlanIntervalEntity target)
    {
        target.Unit = Unit;
        target.Count = Count;
        target.IsDefault = IsDefault;
        target.SortOrder = SortOrder;
    }
}
