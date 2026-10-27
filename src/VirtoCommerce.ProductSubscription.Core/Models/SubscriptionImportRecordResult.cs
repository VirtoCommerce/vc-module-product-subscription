using System;
using System.Collections.Generic;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class SubscriptionImportRecordResult
{
    public string LegacySubscriptionId { get; set; }
    public string LegacySubscriptionNumber { get; set; }
    public string OuterId { get; set; }
    public string ProductId { get; set; }

    /// <summary>One of <see cref="SubscriptionImportOutcome"/>.</summary>
    public string Outcome { get; set; }

    /// <summary>The created subscription, or the existing one for a skipped duplicate. Empty in a dry run.</summary>
    public string SubscriptionId { get; set; }
    public string SubscriptionNumber { get; set; }

    public string Status { get; set; }
    public string PlanId { get; set; }
    public string PlanName { get; set; }
    public DateTime? NextOrderDate { get; set; }

    /// <summary>Why the record failed, or warnings about how it was imported.</summary>
    public IList<string> Messages { get; set; } = new List<string>();
}
