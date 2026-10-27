using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.BackgroundJobs;

/// <summary>
/// The scheduler tick, run by the platform's background job engine on the
/// <c>ProductSubscription.Scheduler.CronExpression</c> schedule while <c>ProductSubscription.Scheduler.Enabled</c> is on.
/// One run at a time across all platform instances, including runs started from the back office.
/// </summary>
public class RenewalSchedulerJob(
    IRenewalProcessor renewalProcessor,
    IDistributedLock distributedLock,
    ILogger<RenewalSchedulerJob> logger)
    : IBackgroundJobHandler<RenewalSchedulerJobPayload>
{
    public const string JobId = "ProductSubscription.RenewalScheduler";
    private const string LockResource = "product-subscription:job:renewal-scheduler";

    public virtual Task Execute(RenewalSchedulerJobPayload payload, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return RunAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>
    /// Runs one tick, waiting up to <paramref name="wait"/> for a tick already running to finish.
    /// Returns the number of renewals processed, or null when the other tick was still running.
    /// </summary>
    public virtual async Task<int?> RunAsync(TimeSpan wait, CancellationToken cancellationToken = default)
    {
        var processed = 0;
        var ran = await distributedLock.TryExecuteAsync(LockResource, async _ => processed = await renewalProcessor.ProcessDueAsync(), wait, cancellationToken);
        if (!ran)
        {
            logger.LogInformation("Skipped the renewal scheduler run: a previous run still holds the lock.");
            return null;
        }

        if (processed > 0)
        {
            logger.LogInformation("Renewal scheduler processed {Count} renewal(s).", processed);
        }

        return processed;
    }
}
