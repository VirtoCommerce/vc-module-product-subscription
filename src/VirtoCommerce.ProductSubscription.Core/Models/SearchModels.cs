using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class SubscriptionPlanSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }
    public bool? IsActive { get; set; }
}

public class SubscriptionPlanSearchResult : GenericSearchResult<SubscriptionPlan>;

public class SubscriptionSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }
    public string CustomerId { get; set; }
    public string OrganizationId { get; set; }
    public IList<string> Statuses { get; set; }
    public string ProductId { get; set; }
    public string PlanId { get; set; }
    public string InitialOrderId { get; set; }
    public string StoredPaymentMethodId { get; set; }
    public string OuterId { get; set; }
    public IList<string> OuterIds { get; set; }

    /// <summary>Only subscriptions whose next order date is on or before this date.</summary>
    public DateTime? NextOrderDateBefore { get; set; }

    /// <summary>Only subscriptions without an open renewal.</summary>
    public bool? WithoutOpenRenewal { get; set; }
}

public class SubscriptionSearchResult : GenericSearchResult<Subscription>;

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

public class RenewalSearchResult : GenericSearchResult<Renewal>;

public class SubscriptionCartItemSearchCriteria : SearchCriteriaBase
{
    public IList<string> CartIds { get; set; }
    public string LineItemId { get; set; }
    public string ProductId { get; set; }
}

public class SubscriptionCartItemSearchResult : GenericSearchResult<SubscriptionCartItem>;
