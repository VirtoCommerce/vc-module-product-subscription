namespace VirtoCommerce.ProductSubscription.Core.Models;

public static class SubscriptionImportOutcome
{
    /// <summary>The subscription was created (or, in a dry run, would be created).</summary>
    public const string Created = "Created";
    /// <summary>A subscription with the same OuterId already exists.</summary>
    public const string SkippedDuplicate = "SkippedDuplicate";
    public const string Failed = "Failed";
}
