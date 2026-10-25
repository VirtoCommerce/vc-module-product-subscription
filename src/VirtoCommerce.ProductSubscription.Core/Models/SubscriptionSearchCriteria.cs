using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

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

    /// <summary>
    /// Only subscriptions without an open renewal: no current renewal, or one that has ended or no longer includes
    /// the subscription.
    /// </summary>
    public bool? WithoutOpenRenewal { get; set; }

    /// <summary>Only subscriptions whose current renewal is this one.</summary>
    public string CurrentRenewalId { get; set; }

    /// <summary>Only paused subscriptions set to resume on or before this date.</summary>
    public DateTime? PausedUntilBefore { get; set; }

    /// <summary>Only subscriptions whose last annual reminder (or start, before the first one) is on or before this date.</summary>
    public DateTime? AnnualReminderDueBefore { get; set; }
}
