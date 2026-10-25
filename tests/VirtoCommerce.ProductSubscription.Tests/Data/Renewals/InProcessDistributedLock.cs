using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.DistributedLock;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>A non-reentrant lock per resource, like the platform's, for tests that run code in parallel.</summary>
public sealed class InProcessDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public ConcurrentQueue<string> Acquired { get; } = new();

    public async Task<IDistributedLockHandle> AcquireAsync(string resource, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        return await TryAcquireAsync(resource, timeout ?? Timeout.InfiniteTimeSpan, cancellationToken)
            ?? throw new TimeoutException($"The lock {resource} stayed busy.");
    }

    public async Task<IDistributedLockHandle> TryAcquireAsync(string resource, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        var semaphore = _semaphores.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(timeout, cancellationToken))
        {
            return null;
        }

        Acquired.Enqueue(resource);
        return new InProcessLockHandle(resource, semaphore);
    }

    /// <summary>Holds <paramref name="resource"/> until the returned handle is disposed, as another platform instance would.</summary>
    public IDistributedLockHandle Hold(string resource)
    {
        return TryAcquireAsync(resource).GetAwaiter().GetResult() ?? throw new InvalidOperationException($"{resource} is already held.");
    }
}
