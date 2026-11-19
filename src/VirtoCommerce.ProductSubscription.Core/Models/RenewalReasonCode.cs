namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// Stable codes for why a renewal needs a review (<see cref="Renewal.ReviewReasonCode"/>).
/// The storefront translates them; the English text in <see cref="Renewal.ReviewReason"/> is for the admin and logs.
/// A solution can add its own codes; the storefront shows a generic text for a code it doesn't know.
/// </summary>
public static class RenewalReviewReasonCode
{
    /// <summary>Some products of the delivery can't be ordered now.</summary>
    public const string ItemsUnavailable = "ITEMS_UNAVAILABLE";

    /// <summary>No shipping method is available for the delivery address.</summary>
    public const string NoShippingMethod = "NO_SHIPPING_METHOD";

    /// <summary>The total is higher than the one in the reminder.</summary>
    public const string PriceIncreased = "PRICE_INCREASED";

    /// <summary>
    /// A configured product can't be ordered with the options of the subscription (an option was removed or ran out).
    /// The renewal never orders a different configuration: the customer skips the delivery or subscribes again.
    /// </summary>
    public const string ConfigurationChanged = "CONFIGURATION_CHANGED";

    public static string[] All { get; } = [ItemsUnavailable, NoShippingMethod, PriceIncreased, ConfigurationChanged];
}

/// <summary>
/// Stable codes for the last payment problem of a renewal (<see cref="Renewal.LastErrorCode"/>).
/// The storefront translates them; the English text in <see cref="Renewal.LastErrorMessage"/> is for the admin and logs.
/// A solution can add its own codes; the storefront shows a generic text for a code it doesn't know.
/// </summary>
public static class RenewalErrorCode
{
    /// <summary>The bank asked the customer to confirm the payment (3-D Secure).</summary>
    public const string AuthenticationRequired = "AUTHENTICATION_REQUIRED";

    /// <summary>No saved card on the subscription: the customer has to pay on the order page.</summary>
    public const string CardRequired = "CARD_REQUIRED";

    /// <summary>The saved card is no longer active and has to be updated.</summary>
    public const string CardNeedsUpdate = "CARD_NEEDS_UPDATE";

    /// <summary>The card was declined for good.</summary>
    public const string CardDeclined = "CARD_DECLINED";

    /// <summary>Temporary decline: the platform charges again at the next attempt date.</summary>
    public const string CardDeclinedWillRetry = "CARD_DECLINED_WILL_RETRY";

    /// <summary>Temporary decline: the payment provider retries on its own.</summary>
    public const string CardDeclinedProviderRetry = "CARD_DECLINED_PROVIDER_RETRY";

    /// <summary>The re-authorization when the order shipped was declined.</summary>
    public const string CardDeclinedOnCapture = "CARD_DECLINED_ON_CAPTURE";

    /// <summary>Every retry was declined.</summary>
    public const string RetriesExhausted = "RETRIES_EXHAUSTED";

    /// <summary>The payment couldn't be collected (e.g. the provider's own retries failed).</summary>
    public const string PaymentFailed = "PAYMENT_FAILED";

    /// <summary>The customer didn't confirm the payment in time.</summary>
    public const string PaymentNotConfirmed = "PAYMENT_NOT_CONFIRMED";

    /// <summary>The payment provider couldn't be reached.</summary>
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";

    public static string[] All { get; } =
    [
        AuthenticationRequired, CardRequired, CardNeedsUpdate, CardDeclined, CardDeclinedWillRetry, CardDeclinedProviderRetry,
        CardDeclinedOnCapture, RetriesExhausted, PaymentFailed, PaymentNotConfirmed, ProviderUnavailable,
    ];
}
