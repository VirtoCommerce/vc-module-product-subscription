namespace VirtoCommerce.ProductSubscription.Data.Import;

/// <summary>The old subscription module's interval, as its JSON export writes it when the value is a number.</summary>
internal enum LegacyPaymentInterval
{
    Days,
    Weeks,
    Months,
    Years,
}
