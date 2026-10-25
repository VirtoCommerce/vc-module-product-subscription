using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.BackgroundJobs;
using VirtoCommerce.ProductSubscription.Tests.Data.Renewals;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.BackgroundJobs;

[Trait("Category", "Unit")]
public class RenewalSchedulerJobTests
{
    private const string LockResource = "product-subscription:job:renewal-scheduler";

    private readonly Mock<IRenewalProcessor> _processor = new();
    private readonly InProcessDistributedLock _lock = new();
    private readonly RenewalSchedulerJob _job;

    public RenewalSchedulerJobTests()
    {
        _processor.Setup(x => x.ProcessDueAsync()).ReturnsAsync(3);
        _job = new RenewalSchedulerJob(_processor.Object, _lock, NullLogger<RenewalSchedulerJob>.Instance);
    }

    [Fact]
    public async Task Execute_RunsOneSchedulerTick()
    {
        await _job.Execute(new RenewalSchedulerJobPayload(), Mock.Of<IJobExecutionContext>(), TestContext.Current.CancellationToken);

        _processor.Verify(x => x.ProcessDueAsync(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReturnsTheNumberProcessed()
    {
        var processed = await _job.RunAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        processed.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_WhileAnotherTickRuns_SkipsWithoutProcessing()
    {
        // A run from the back office used to skip this lock and overlap the scheduled tick.
        using (_lock.Hold(LockResource))
        {
            var processed = await _job.RunAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

            processed.Should().BeNull();
        }

        _processor.Verify(x => x.ProcessDueAsync(), Times.Never);
    }
}
