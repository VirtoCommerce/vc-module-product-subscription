using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class SubscriptionPlanSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }
    public bool? IsActive { get; set; }
}
