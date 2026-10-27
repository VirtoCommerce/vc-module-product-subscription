using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class RenewalItemEntity : Entity, IDataEntity<RenewalItemEntity, RenewalItem>
{
    [Required]
    [StringLength(128)]
    public string RenewalId { get; set; }
    public virtual RenewalEntity Renewal { get; set; }

    [Required]
    [StringLength(128)]
    public string SubscriptionId { get; set; }

    [Required]
    [StringLength(128)]
    public string ProductId { get; set; }

    [StringLength(128)]
    public string Sku { get; set; }

    [StringLength(1024)]
    public string ProductName { get; set; }

    [StringLength(2048)]
    public string ImageUrl { get; set; }

    public int Quantity { get; set; }
    public decimal ListPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsExcluded { get; set; }

    [StringLength(1024)]
    public string Message { get; set; }

    public int SortOrder { get; set; }

    public virtual RenewalItem ToModel(RenewalItem model)
    {
        model.Id = Id;
        model.SubscriptionId = SubscriptionId;
        model.ProductId = ProductId;
        model.Sku = Sku;
        model.ProductName = ProductName;
        model.ImageUrl = ImageUrl;
        model.Quantity = Quantity;
        model.ListPrice = ListPrice;
        model.DiscountAmount = DiscountAmount;
        model.Total = Total;
        model.IsAvailable = IsAvailable;
        model.IsExcluded = IsExcluded;
        model.Message = Message;
        return model;
    }

    public virtual RenewalItemEntity FromModel(RenewalItem model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);
        Id = model.Id;
        SubscriptionId = model.SubscriptionId;
        ProductId = model.ProductId;
        Sku = model.Sku;
        ProductName = model.ProductName;
        ImageUrl = model.ImageUrl;
        Quantity = model.Quantity;
        ListPrice = model.ListPrice;
        DiscountAmount = model.DiscountAmount;
        Total = model.Total;
        IsAvailable = model.IsAvailable;
        IsExcluded = model.IsExcluded;
        Message = model.Message;
        return this;
    }

    public virtual void Patch(RenewalItemEntity target)
    {
        target.SubscriptionId = SubscriptionId;
        target.ProductId = ProductId;
        target.Sku = Sku;
        target.ProductName = ProductName;
        target.ImageUrl = ImageUrl;
        target.Quantity = Quantity;
        target.ListPrice = ListPrice;
        target.DiscountAmount = DiscountAmount;
        target.Total = Total;
        target.IsAvailable = IsAvailable;
        target.IsExcluded = IsExcluded;
        target.Message = Message;
        target.SortOrder = SortOrder;
    }
}
