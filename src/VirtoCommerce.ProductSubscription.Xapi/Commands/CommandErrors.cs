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
    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SubscriptionOperationException ex)
        {
            throw new ExecutionError(ex.Message) { Code = "SUBSCRIPTION_ERROR" };
        }
    }
}
