using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.DistributedLock;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

public sealed class InProcessLockHandle(string resource, SemaphoreSlim semaphore) : IDistributedLockHandle
{
    private int _released;

    public string Resource { get; } = resource;

    public CancellationToken HandleLostToken => CancellationToken.None;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            semaphore.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
