using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class SubscriptionCartItemSearchCriteria : SearchCriteriaBase
{
    public IList<string> CartIds { get; set; }
    public string LineItemId { get; set; }
    public string ProductId { get; set; }
}
