using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Models;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>The scheduler finds every due subscription, however many newer ones exist.</summary>
[Trait("Category", "Unit")]
public class RenewalSchedulerSelectionTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task ProcessDue_RenewsTheOldestDueSubscription_PastManyNewerOnesNotDueYet()
    {
        // 600 newer subscriptions inside the old 60-day window filled the old 500-row page.
        for (var i = 0; i < 600; i++)
        {
            _harness.AddSubscription($"customer-{i}", _harness.Now.AddDays(30), x => x.CreatedDate = _harness.Now.AddMinutes(-i));
        }

        var oldest = _harness.AddSubscription("customer-old", _harness.Now, x => x.CreatedDate = _harness.Now.AddYears(-1));

        await _harness.Processor.ProcessDueAsync();

        _harness.GetRenewals().Should().ContainSingle()
            .Which.Items.Should().ContainSingle(x => x.SubscriptionId == oldest.Id);
    }

    [Fact]
    public async Task ProcessDue_RenewsEveryDueSubscription_AcrossPages()
    {
        var due = Enumerable.Range(0, 1100)
            .Select(i => _harness.AddSubscription($"customer-{i}", _harness.Now.AddDays(1), x => x.CreatedDate = _harness.Now.AddMinutes(-i)))
            .ToList();

        await _harness.Processor.ProcessDueAsync();

        var renewals = _harness.GetRenewals();
        renewals.Should().HaveCount(due.Count);
        renewals.SelectMany(x => x.Items).Select(x => x.SubscriptionId).Should().BeEquivalentTo(due.Select(x => x.Id));
    }

    [Fact]
    public async Task ProcessDue_GroupsOneCustomersSubscriptionsDueTheSameDay_IntoOneRenewal()
    {
        var first = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1));
        var second = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x => x.ProductId = "product-2");

        await _harness.Processor.ProcessDueAsync();

        _harness.GetRenewals().Should().ContainSingle()
            .Which.Items.Select(x => x.SubscriptionId).Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task ProcessDue_DoesNotRenewSubscriptionsOutsideTheStoreLeadDays()
    {
        _harness.AddSubscription("customer-1", _harness.Now.AddDays(4));

        await _harness.Processor.ProcessDueAsync();

        _harness.GetRenewals().Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDue_SkipsACustomerWhoseLockIsBusy_AndRenewsTheOthers()
    {
        var busy = _harness.AddSubscription("customer-busy", _harness.Now.AddDays(1));
        var free = _harness.AddSubscription("customer-free", _harness.Now.AddDays(1));

        using (_harness.Lock.Hold("product-subscription:customer:customer-busy"))
        {
            await _harness.Processor.ProcessDueAsync();
        }

        _harness.GetRenewalsOf(free.Id).Should().ContainSingle();
        _harness.GetRenewalsOf(busy.Id).Should().BeEmpty();

        // The next run picks the busy customer up.
        await _harness.Processor.ProcessDueAsync();
        _harness.GetRenewalsOf(busy.Id).Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessDue_SendsTheAnnualReminderToEveryDueSubscription_PastManyNewerOnes()
    {
        for (var i = 0; i < 230; i++)
        {
            _harness.AddSubscription($"customer-{i}", _harness.Now.AddDays(20), x => x.CreatedDate = _harness.Now.AddMinutes(-i));
        }

        var due = Enumerable.Range(0, 20)
            .Select(i => _harness.AddSubscription($"customer-old-{i}", _harness.Now.AddDays(20), x =>
            {
                x.CreatedDate = _harness.Now.AddYears(-2);
                x.StartDate = _harness.Now.AddYears(-2);
                x.LastAnnualReminderDate = _harness.Now.AddMonths(-13);
            }))
            .ToList();

        await _harness.Processor.ProcessDueAsync();

        due.Select(x => _harness.GetSubscription(x.Id).LastAnnualReminderDate).Should().AllBeEquivalentTo(_harness.Now);
    }

    [Fact]
    public async Task ProcessDue_ResumesAPausedSubscriptionAtItsDate_PastManyNewerPausedOnes()
    {
        for (var i = 0; i < 600; i++)
        {
            _harness.AddSubscription($"customer-{i}", _harness.Now.AddDays(40), x =>
            {
                x.CreatedDate = _harness.Now.AddMinutes(-i);
                x.Status = SubscriptionStatus.Paused;
                x.PausedUntil = _harness.Now.AddDays(10);
            });
        }

        var due = _harness.AddSubscription("customer-old", _harness.Now.AddDays(40), x =>
        {
            x.CreatedDate = _harness.Now.AddYears(-1);
            x.Status = SubscriptionStatus.Paused;
            x.PausedUntil = _harness.Now.AddHours(-1);
        });

        await _harness.Processor.ProcessDueAsync();

        var resumed = _harness.GetSubscription(due.Id);
        resumed.Status.Should().Be(SubscriptionStatus.Active);
        resumed.PausedUntil.Should().BeNull();
    }
}
