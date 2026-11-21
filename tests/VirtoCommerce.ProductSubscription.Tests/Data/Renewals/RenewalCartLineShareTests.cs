using FluentAssertions;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>Subscriptions to one product share its cart line: each renewal item gets its share of the line, not all of it.</summary>
[Trait("Category", "Unit")]
public class RenewalCartLineShareTests
{
    [Fact]
    public void OneSubscription_GetsTheWholeLine()
    {
        TestableRenewalCartBuilder.Split(3, 30m, 3).Should().Equal(30m);
    }

    [Fact]
    public void TwoSubscriptions_ShareTheLineByQuantity()
    {
        TestableRenewalCartBuilder.Split(4, 40m, 1, 3).Should().Equal(10m, 30m);
    }

    [Fact]
    public void Shares_AlwaysAddUpToTheLineTotal()
    {
        var shares = TestableRenewalCartBuilder.Split(3, 10m, 1, 1, 1);

        shares.Should().Equal(3.33m, 3.33m, 3.34m);
    }
}
