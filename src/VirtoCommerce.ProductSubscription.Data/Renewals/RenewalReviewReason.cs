namespace VirtoCommerce.ProductSubscription.Data.Renewals;

/// <summary>Why a renewal needs the customer's review.</summary>
/// <param name="Code">Stable code the storefront translates (<see cref="Core.Models.RenewalReviewReasonCode"/>).</param>
/// <param name="Message">The same in English, for the admin and logs.</param>
public record RenewalReviewReason(string Code, string Message);
