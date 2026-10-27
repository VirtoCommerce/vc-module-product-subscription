using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class RenewalItem : Entity, ICloneable
{
    public string SubscriptionId { get; set; }
    public string ProductId { get; set; }
    public string Sku { get; set; }
    public string ProductName { get; set; }
    public string ImageUrl { get; set; }
    public int Quantity { get; set; }

    public decimal ListPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Line total after discounts, before tax.</summary>
    public decimal Total { get; set; }

    public bool IsAvailable { get; set; } = true;
    public string Message { get; set; }

    /// <summary>Excluded from this renewal only (e.g. out of stock); the subscription continues.</summary>
    public bool IsExcluded { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
