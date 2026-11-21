using System;

namespace VirtoCommerce.ProductSubscription.Core.Exceptions;

/// <summary>
/// A customer asked for a subscription action the store has turned off in its "Customer actions" settings.
/// </summary>
public class SubscriptionActionDisabledException : SubscriptionOperationException
{
    public SubscriptionActionDisabledException()
        : base("This store doesn't let customers do this. Please contact the store.")
    {
    }

    public SubscriptionActionDisabledException(string message)
        : base(message)
    {
    }

    public SubscriptionActionDisabledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
