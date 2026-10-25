namespace VirtoCommerce.ProductSubscription.Core.Models;

public static class SubscriptionStatus
{
    /// <summary>Created from an order whose payment is not authorized yet.</summary>
    public const string PendingActivation = "PendingActivation";
    public const string Active = "Active";
    public const string Paused = "Paused";
    /// <summary>A renewal could not be paid; renewals stop until the payment method is fixed.</summary>
    public const string PastDue = "PastDue";
    public const string Cancelled = "Cancelled";

    public static string[] All { get; } = [PendingActivation, Active, Paused, PastDue, Cancelled];
}
