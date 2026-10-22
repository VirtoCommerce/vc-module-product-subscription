using System;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class SubscriptionCartItemEntity : AuditableEntity, IDataEntity<SubscriptionCartItemEntity, SubscriptionCartItem>
{
    [StringLength(128)]
    public string StoreId { get; set; }

    [Required]
    [StringLength(128)]
    public string CartId { get; set; }

    [Required]
    [StringLength(128)]
    public string LineItemId { get; set; }

    [Required]
    [StringLength(128)]
    public string ProductId { get; set; }

    [StringLength(128)]
    public string CustomerId { get; set; }

    [Required]
    [StringLength(128)]
    public string PlanId { get; set; }

    [Required]
    [StringLength(16)]
    public string IntervalUnit { get; set; }

    public int IntervalCount { get; set; }

    public virtual SubscriptionCartItem ToModel(SubscriptionCartItem model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;
        model.StoreId = StoreId;
        model.CartId = CartId;
        model.LineItemId = LineItemId;
        model.ProductId = ProductId;
        model.CustomerId = CustomerId;
        model.PlanId = PlanId;
        model.IntervalUnit = EnumUtility.SafeParse(IntervalUnit, SubscriptionIntervalUnit.Month);
        model.IntervalCount = IntervalCount;
        return model;
    }

    public virtual SubscriptionCartItemEntity FromModel(SubscriptionCartItem model, PrimaryKeyResolvingMap pkMap)
    {
        ArgumentNullException.ThrowIfNull(model);

        pkMap.AddPair(model, this);
        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;
        StoreId = model.StoreId;
        CartId = model.CartId;
        LineItemId = model.LineItemId;
        ProductId = model.ProductId;
        CustomerId = model.CustomerId;
        PlanId = model.PlanId;
        IntervalUnit = model.IntervalUnit.ToString();
        IntervalCount = Math.Max(1, model.IntervalCount);
        return this;
    }

    public virtual void Patch(SubscriptionCartItemEntity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.StoreId = StoreId;
        target.CartId = CartId;
        target.LineItemId = LineItemId;
        target.ProductId = ProductId;
        target.CustomerId = CustomerId;
        target.PlanId = PlanId;
        target.IntervalUnit = IntervalUnit;
        target.IntervalCount = IntervalCount;
    }
}
