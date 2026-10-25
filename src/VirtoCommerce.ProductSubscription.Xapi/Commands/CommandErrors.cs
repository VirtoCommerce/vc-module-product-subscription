using System;
using System.Threading.Tasks;
using GraphQL;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

internal static class CommandErrors
{
    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            throw new ExecutionError(ex.Message) { Code = "SUBSCRIPTION_ERROR" };
        }
    }
}
