using System;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.ProductSubscription.Core.Exceptions;

namespace VirtoCommerce.ProductSubscription.Data.Services;

/// <summary>
/// One lock per customer serializes every change to that customer's subscriptions and renewals: the scheduler,
/// renewal processing, order events and customer or admin actions. A renewal only holds one customer's
/// subscriptions, so the same lock covers it, and no code path ever needs two locks (or an order to take them in).
/// The lock is not reentrant: code that already holds it calls the unlocked variants.
/// </summary>
public static class SubscriptionLocks
{
    public static TimeSpan DefaultWait { get; } = TimeSpan.FromSeconds(30);

    public static string ForCustomer(string customerId) => $"product-subscription:customer:{customerId}";

    /// <summary>Runs <paramref name="action"/> under the customer's lock; throws when the lock stays busy for <paramref name="wait"/>.</summary>
    public static async Task<T> RunForCustomerAsync<T>(this IDistributedLock distributedLock, string customerId, Func<Task<T>> action, TimeSpan? wait = null)
    {
        var handle = await distributedLock.TryAcquireAsync(ForCustomer(customerId), wait ?? DefaultWait)
            ?? throw new SubscriptionOperationException("Your subscriptions are being updated right now. Please try again in a moment.");

        await using (handle)
        {
            return await action();
        }
    }

    /// <summary>Runs <paramref name="action"/> under the customer's lock; throws when the lock stays busy for <paramref name="wait"/>.</summary>
    public static Task RunForCustomerAsync(this IDistributedLock distributedLock, string customerId, Func<Task> action, TimeSpan? wait = null)
    {
        return distributedLock.RunForCustomerAsync(customerId, async () =>
        {
            await action();
            return true;
        }, wait);
    }

    /// <summary>Runs <paramref name="action"/> under the customer's lock; returns false when the lock stays busy for <paramref name="wait"/>.</summary>
    public static async Task<bool> TryRunForCustomerAsync(this IDistributedLock distributedLock, string customerId, Func<Task> action, TimeSpan? wait = null)
    {
        var handle = await distributedLock.TryAcquireAsync(ForCustomer(customerId), wait ?? DefaultWait);
        if (handle == null)
        {
            return false;
        }

        await using (handle)
        {
            await action();
        }

        return true;
    }
}
