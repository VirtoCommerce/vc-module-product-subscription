using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class RenewalSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }
    public string CustomerId { get; set; }
    public IList<string> States { get; set; }
    public string SubscriptionId { get; set; }
    public string OrderId { get; set; }

    /// <summary>Only renewals the scheduler should process at or before this moment.</summary>
    public DateTime? DueBefore { get; set; }

    public bool? OnlyOpen { get; set; }
}
