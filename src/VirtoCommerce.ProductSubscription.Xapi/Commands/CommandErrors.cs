using System;
using System.Threading.Tasks;
using GraphQL;
using VirtoCommerce.ProductSubscription.Core.Exceptions;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

/// <summary>
/// Shows the customer the message of a subscription action that can't be done. Any other exception is left to the
/// GraphQL error handling, so internal messages (data access, framework) never reach the storefront.
/// </summary>
internal static class CommandErrors
{
    /// <summary>The store has turned this action off for customers (its "Customer actions" settings).</summary>
    public const string ActionDisabledCode = "SUBSCRIPTION_ACTION_DISABLED";

    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SubscriptionActionDisabledException ex)
        {
            throw new ExecutionError(ex.Message) { Code = ActionDisabledCode };
        }
        catch (SubscriptionOperationException ex)
        {
            throw new ExecutionError(ex.Message) { Code = "SUBSCRIPTION_ERROR" };
        }
    }
}
