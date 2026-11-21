using System;

namespace VirtoCommerce.ProductSubscription.Core.Exceptions;

/// <summary>
/// A subscription action that can't be done right now. The message is written for the customer or administrator who
/// asked for it, so the storefront and the back office show it as is; any other exception is reported as a server error.
/// </summary>
public class SubscriptionOperationException : InvalidOperationException
{
    public SubscriptionOperationException()
    {
    }

    public SubscriptionOperationException(string message)
        : base(message)
    {
    }

    public SubscriptionOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
