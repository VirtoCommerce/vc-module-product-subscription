namespace VirtoCommerce.ProductSubscription.Core.Models;

public static class RenewalEventType
{
    public const string StateChanged = "StateChanged";
    public const string ReminderSent = "ReminderSent";
    public const string Calculated = "Calculated";
    public const string PaymentAttempt = "PaymentAttempt";
    public const string Captured = "Captured";
    public const string CustomerAction = "CustomerAction";
    public const string Error = "Error";
}
