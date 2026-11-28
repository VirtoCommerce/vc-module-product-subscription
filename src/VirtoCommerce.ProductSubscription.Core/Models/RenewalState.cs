namespace VirtoCommerce.ProductSubscription.Core.Models;

public static class RenewalState
{
    public const string Scheduled = "Scheduled";
    public const string Notified = "Notified";
    public const string ReviewRequired = "ReviewRequired";
    public const string Ordered = "Ordered";
    public const string PaymentActionRequired = "PaymentActionRequired";
    public const string RetryScheduled = "RetryScheduled";
    public const string Authorized = "Authorized";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
    public const string Cancelled = "Cancelled";

    public static string[] All { get; } =
        [Scheduled, Notified, ReviewRequired, Ordered, PaymentActionRequired, RetryScheduled, Authorized, Completed, Failed, Skipped, Cancelled];

    public static string[] Final { get; } = [Completed, Failed, Skipped, Cancelled];

    /// <summary>States in which the order has not been placed yet, so the customer can still change or skip the renewal.</summary>
    public static string[] BeforeOrder { get; } = [Scheduled, Notified, ReviewRequired];
}
