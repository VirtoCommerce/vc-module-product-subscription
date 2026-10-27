using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.XCart.Core;

namespace VirtoCommerce.ProductSubscription.Data.Renewals;

public class RenewalCartResult
{
    public CartAggregate Aggregate { get; set; }
    public IList<RenewalItem> Items { get; set; } = new List<RenewalItem>();
    public IList<string> Problems { get; set; } = new List<string>();
    public bool HasUnavailableItems => Items.Any(x => !x.IsAvailable && !x.IsExcluded);
    public decimal Total { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }
}
