using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Models;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>Changes to one customer's subscriptions never race: no second renewal, no lost update.</summary>
[Trait("Category", "Unit")]
public class RenewalConcurrencyTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task OrderNow_TwiceAtOnce_PlacesOneOrder()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(20));

        var first = Task.Run(() => _harness.Processor.OrderNowAsync(subscription.Id));
        var second = Task.Run(() => _harness.Processor.OrderNowAsync(subscription.Id));
        var results = await Task.WhenAll(Capture(first), Capture(second));

        _harness.GetRenewals().Should().ContainSingle();
        _harness.PaymentMethod.Requests.Should().ContainSingle();
        results.Count(x => x == null).Should().Be(1);
        results.Single(x => x != null).Should().BeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public async Task OrderNow_WhileTheSchedulerRuns_MakesOneRenewal()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1));

        var scheduler = Task.Run(() => _harness.Processor.ProcessDueAsync());
        var orderNow = Capture(Task.Run(() => _harness.Processor.OrderNowAsync(subscription.Id)));
        await Task.WhenAll(scheduler, orderNow);

        _harness.GetRenewalsOf(subscription.Id).Should().ContainSingle();
        _harness.PaymentMethod.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Cancel_DuringOrderPlacement_IsNotOverwritten()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);
        Task cancel = null;
        _harness.OnPlaceOrder = async _ =>
        {
            // The customer cancels while the renewal order is being placed. It used to be saved at once, then
            // overwritten with the stale copy the renewal saved after the order.
            cancel = Task.Run(() => _harness.Manager.CancelAsync(subscription.Id, "Too much coffee"));
            await Task.Delay(200, TestContext.Current.CancellationToken);
        };

        await _harness.Processor.ProcessDueAsync();
        await cancel;

        var saved = _harness.GetSubscription(subscription.Id);
        saved.Status.Should().Be(SubscriptionStatus.Cancelled);
        saved.CancelReason.Should().Be("Too much coffee");
        saved.OrdersCount.Should().Be(1, "the renewal placed before the cancel still counts");
    }

    [Fact]
    public async Task Pause_WhileTheCustomerLockIsBusy_WaitsInsteadOfRacing()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(20));

        Task pause;
        using (_harness.Lock.Hold("product-subscription:customer:customer-1"))
        {
            pause = Task.Run(() => _harness.Manager.PauseAsync(subscription.Id, null));
            await Task.Delay(100, TestContext.Current.CancellationToken);
            pause.IsCompleted.Should().BeFalse();
        }

        await pause;
        _harness.GetSubscription(subscription.Id).Status.Should().Be(SubscriptionStatus.Paused);
    }

    private static async Task<Exception> Capture(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
