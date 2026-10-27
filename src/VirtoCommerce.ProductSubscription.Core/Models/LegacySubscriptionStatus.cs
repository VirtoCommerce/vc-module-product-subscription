namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>Statuses of the old subscription module.</summary>
public enum LegacySubscriptionStatus
{
    Active,
    Trialing,
    PastDue,
    Cancelled,
    Unpaid,
}
