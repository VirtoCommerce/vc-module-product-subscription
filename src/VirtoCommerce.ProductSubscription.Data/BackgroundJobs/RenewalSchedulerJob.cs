using System;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.BackgroundJobs;

/// <summary>
/// The scheduler tick: one run at a time across all platform instances.
/// </summary>
public class RenewalSchedulerJob(
    IRenewalProcessor renewalProcessor,
    ISettingsManager settingsManager,
    IDistributedLock distributedLock,
    ILogger<RenewalSchedulerJob> logger)
{
    public const string JobId = "ProductSubscription.RenewalScheduler";
    private const string LockResource = "product-subscription:job:renewal-scheduler";

    [DisableConcurrentExecution(60)]
    public virtual async Task RunAsync()
    {
        if (!await settingsManager.GetValueAsync<bool>(ModuleConstants.Settings.General.SchedulerEnabled))
        {
            return;
        }

        var processed = 0;
        var ran = await distributedLock.TryExecuteAsync(LockResource, async _ => processed = await renewalProcessor.ProcessDueAsync(), TimeSpan.FromSeconds(5));
        if (!ran)
        {
            logger.LogInformation("Skipped the renewal scheduler run: a previous run still holds the lock.");
        }
        else if (processed > 0)
        {
            logger.LogInformation("Renewal scheduler processed {Count} renewal(s).", processed);
        }
    }

    public static void Schedule(IRecurringJobManager recurringJobManager, string cronExpression)
    {
        recurringJobManager.AddOrUpdate<RenewalSchedulerJob>(JobId, x => x.RunAsync(), string.IsNullOrWhiteSpace(cronExpression) ? "*/5 * * * *" : cronExpression);
    }
}
