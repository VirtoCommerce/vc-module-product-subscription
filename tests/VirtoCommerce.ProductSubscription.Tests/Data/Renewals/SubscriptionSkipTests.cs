using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// A skip names the delivery the customer saw. A page left open after another skip must not skip a second delivery.
/// </summary>
[Trait("Category", "Unit")]
public class SubscriptionSkipTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task SkipFromAStalePage_IsRefused_AndSkipsNothingMore()
    {
        var shown = _harness.Now.AddDays(20);
        var subscription = _harness.AddSubscription("customer-1", shown);

        await _harness.Manager.SkipNextAsync(subscription.Id, shown);
        var afterFirstSkip = _harness.GetSubscription(subscription.Id).NextOrderDate;

        var staleSkip = () => _harness.Manager.SkipNextAsync(subscription.Id, shown);

        await staleSkip.Should().ThrowAsync<SubscriptionOperationException>().WithMessage("*changed*");
        var current = _harness.GetSubscription(subscription.Id);
        current.NextOrderDate.Should().Be(afterFirstSkip);
        current.SkipCount.Should().Be(1);
    }

    [Fact]
    public async Task SkipWithoutTheShownDate_StillSkips()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(20));

        await _harness.Manager.SkipNextAsync(subscription.Id);

        _harness.GetSubscription(subscription.Id).SkipCount.Should().Be(1);
    }
}
