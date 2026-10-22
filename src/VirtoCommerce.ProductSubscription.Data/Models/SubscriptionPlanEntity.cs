using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class SubscriptionPlanEntity : AuditableEntity, IDataEntity<SubscriptionPlanEntity, SubscriptionPlan>
{
    [StringLength(128)]
    public string StoreId { get; set; }

    [Required]
    [StringLength(256)]
    public string Name { get; set; }

    [StringLength(2048)]
    public string Description { get; set; }

    public bool IsActive { get; set; }
    public int Priority { get; set; }

    [StringLength(32)]
    public string DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public virtual ObservableCollection<SubscriptionPlanIntervalEntity> Intervals { get; set; } = new NullCollection<SubscriptionPlanIntervalEntity>();
    public virtual ObservableCollection<SubscriptionPlanAssignmentEntity> Assignments { get; set; } = new NullCollection<SubscriptionPlanAssignmentEntity>();

    public virtual SubscriptionPlan ToModel(SubscriptionPlan model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.StoreId = StoreId;
        model.Name = Name;
        model.Description = Description;
        model.IsActive = IsActive;
        model.Priority = Priority;
        model.DiscountType = EnumUtility.SafeParse(DiscountType, SubscriptionDiscountType.None);
        model.DiscountValue = DiscountValue;

        model.Intervals = Intervals.OrderBy(x => x.SortOrder).Select(x => x.ToModel(AbstractTypeFactory<SubscriptionPlanInterval>.TryCreateInstance())).ToList();
        model.ProductIds = Assignments.Where(x => x.ObjectType == SubscriptionPlanAssignmentEntity.ProductObjectType).Select(x => x.ObjectId).ToList();
        model.CategoryIds = Assignments.Where(x => x.ObjectType == SubscriptionPlanAssignmentEntity.CategoryObjectType).Select(x => x.ObjectId).ToList();

        return model;
    }

    public virtual SubscriptionPlanEntity FromModel(SubscriptionPlan model, PrimaryKeyResolvingMap pkMap)
    {
        ArgumentNullException.ThrowIfNull(model);

        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        StoreId = model.StoreId;
        Name = model.Name;
        Description = model.Description;
        IsActive = model.IsActive;
        Priority = model.Priority;
        DiscountType = model.DiscountType.ToString();
        DiscountValue = model.DiscountValue;

        if (model.Intervals != null)
        {
            Intervals = new ObservableCollection<SubscriptionPlanIntervalEntity>(model.Intervals.Select((x, i) =>
            {
                var entity = AbstractTypeFactory<SubscriptionPlanIntervalEntity>.TryCreateInstance().FromModel(x, pkMap);
                entity.SortOrder = i;
                return entity;
            }));
        }

        if (model.ProductIds != null || model.CategoryIds != null)
        {
            var assignments = (model.ProductIds ?? []).Distinct()
                .Select(x => new SubscriptionPlanAssignmentEntity { ObjectType = SubscriptionPlanAssignmentEntity.ProductObjectType, ObjectId = x })
                .Concat((model.CategoryIds ?? []).Distinct()
                    .Select(x => new SubscriptionPlanAssignmentEntity { ObjectType = SubscriptionPlanAssignmentEntity.CategoryObjectType, ObjectId = x }));
            Assignments = new ObservableCollection<SubscriptionPlanAssignmentEntity>(assignments);
        }

        return this;
    }

    public virtual void Patch(SubscriptionPlanEntity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.StoreId = StoreId;
        target.Name = Name;
        target.Description = Description;
        target.IsActive = IsActive;
        target.Priority = Priority;
        target.DiscountType = DiscountType;
        target.DiscountValue = DiscountValue;

        if (!Intervals.IsNullCollection())
        {
            Intervals.Patch(target.Intervals, (source, destination) => source.Patch(destination));
        }

        if (!Assignments.IsNullCollection())
        {
            var comparer = AnonymousComparer.Create((SubscriptionPlanAssignmentEntity x) => $"{x.ObjectType}|{x.ObjectId}");
            Assignments.Patch(target.Assignments, comparer, (source, destination) => { });
        }
    }
}

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

public class SubscriptionPlanAssignmentEntity : Entity
{
    public const string ProductObjectType = "Product";
    public const string CategoryObjectType = "Category";

    [Required]
    [StringLength(128)]
    public string PlanId { get; set; }
    public virtual SubscriptionPlanEntity Plan { get; set; }

    [Required]
    [StringLength(32)]
    public string ObjectType { get; set; }

    [Required]
    [StringLength(128)]
    public string ObjectId { get; set; }
}
