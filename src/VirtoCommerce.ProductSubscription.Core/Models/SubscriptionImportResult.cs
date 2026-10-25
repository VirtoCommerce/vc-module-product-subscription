using System.Collections.Generic;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public class SubscriptionImportResult
{
    public bool DryRun { get; set; }
    public int TotalCount { get; set; }

    /// <summary>Subscriptions created; in a dry run, subscriptions that would be created.</summary>
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    public IList<SubscriptionImportRecordResult> Records { get; set; } = new List<SubscriptionImportRecordResult>();
}
