using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Services;

/// <summary>The server tells which orders a renewal placed and which subscriptions their lines belong to.</summary>
[Trait("Category", "Unit")]
public class OrderSubscriptionInfoServiceTests
{
    private const string Customer = "customer";

    private readonly Mock<ISubscriptionSearchService> _subscriptionSearch = new();
    private readonly Mock<IRenewalSearchService> _renewalSearch = new();
    private readonly Mock<ISubscriptionService> _subscriptionService = new();
    private readonly List<Subscription> _subscriptions = [];
    private readonly List<Renewal> _renewals = [];
    private SubscriptionSearchCriteria _subscriptionCriteria;
    private RenewalSearchCriteria _renewalCriteria;

    public OrderSubscriptionInfoServiceTests()
    {
        _subscriptionSearch
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((SubscriptionSearchCriteria c, bool _) =>
            {
                _subscriptionCriteria = c;
                var all = _subscriptions.Where(x => c.InitialOrderIds.Contains(x.InitialOrderId) && (c.CustomerId == null || x.CustomerId == c.CustomerId)).ToList();
                return new SubscriptionSearchResult { Results = all.Skip(c.Skip).Take(c.Take).ToList(), TotalCount = all.Count };
            });
        _renewalSearch
            .Setup(x => x.SearchAsync(It.IsAny<RenewalSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((RenewalSearchCriteria c, bool _) =>
            {
                _renewalCriteria = c;
                var all = _renewals.Where(x => c.OrderIds.Contains(x.OrderId) && (c.CustomerId == null || x.CustomerId == c.CustomerId)).ToList();
                return new RenewalSearchResult { Results = all.Skip(c.Skip).Take(c.Take).ToList(), TotalCount = all.Count };
            });
        _subscriptionService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => _subscriptions.Where(x => ids.Contains(x.Id)).ToList());
    }

    private OrderSubscriptionInfoService Service() => new(_subscriptionSearch.Object, _renewalSearch.Object, _subscriptionService.Object);

    private Subscription AddSubscription(string id, string productId, string initialOrderId = "first-order", string customerId = Customer)
    {
        var subscription = new Subscription
        {
            Id = id,
            Number = $"SUB-{id}",
            ProductId = productId,
            InitialOrderId = initialOrderId,
            CustomerId = customerId,
            IntervalUnit = SubscriptionIntervalUnit.Week,
            IntervalCount = 2,
            Status = SubscriptionStatus.Active,
        };
        _subscriptions.Add(subscription);
        return subscription;
    }

    [Fact]
    public async Task A_renewal_order_names_its_renewal_and_the_subscriptions_it_delivered()
    {
        AddSubscription("s1", "pepsi");
        AddSubscription("s2", "lipton");
        _renewals.Add(new Renewal
        {
            Id = "r1",
            Number = "REN-1",
            OrderId = "renewal-order",
            CustomerId = Customer,
            ScheduledDate = new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc),
            Items =
            [
                new RenewalItem { SubscriptionId = "s1", ProductId = "pepsi" },
                new RenewalItem { SubscriptionId = "s2", ProductId = "lipton", IsExcluded = true },
            ],
        });

        var result = await Service().GetForOrdersAsync(["renewal-order"], Customer);

        var info = result.Should().ContainSingle().Subject;
        info.OrderId.Should().Be("renewal-order");
        info.RenewalNumber.Should().Be("REN-1");
        info.RenewalScheduledDate.Should().Be(new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc));
        // a line left out of the delivery isn't in the order
        info.Subscriptions.Should().ContainSingle(x => x.SubscriptionId == "s1" && x.ProductId == "pepsi" && !x.IsStartedByOrder && x.SubscriptionNumber == "SUB-s1");
    }

    [Fact]
    public async Task A_checkout_order_lists_the_subscriptions_it_started()
    {
        AddSubscription("s1", "pepsi", initialOrderId: "checkout");
        AddSubscription("s2", "lipton", initialOrderId: "checkout");

        var info = (await Service().GetForOrdersAsync(["checkout"], Customer)).Single();

        info.RenewalId.Should().BeNull();
        info.Subscriptions.Select(x => x.ProductId).Should().BeEquivalentTo("pepsi", "lipton");
        info.Subscriptions.Should().OnlyContain(x => x.IsStartedByOrder && x.IntervalUnit == SubscriptionIntervalUnit.Week && x.IntervalCount == 2);
    }

    [Fact]
    public async Task A_page_of_orders_takes_two_searches_and_orders_without_subscriptions_are_left_out()
    {
        AddSubscription("s1", "pepsi", initialOrderId: "o1");
        _renewals.Add(new Renewal { Id = "r1", Number = "REN-1", OrderId = "o3", CustomerId = Customer, Items = [new RenewalItem { SubscriptionId = "s1", ProductId = "pepsi" }] });

        var result = await Service().GetForOrdersAsync(["o1", "o2", "o3"], Customer);

        result.Select(x => x.OrderId).Should().Equal("o1", "o3");
        _subscriptionSearch.Verify(x => x.SearchAsync(It.IsAny<SubscriptionSearchCriteria>(), It.IsAny<bool>()), Times.Once);
        _renewalSearch.Verify(x => x.SearchAsync(It.IsAny<RenewalSearchCriteria>(), It.IsAny<bool>()), Times.Once);
        // s1 came with its first order: not loaded again for the renewal
        _subscriptionService.Verify(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Every_subscription_of_a_big_checkout_is_listed_not_only_the_first_page()
    {
        // a bulk B2B checkout: more subscribed lines than one search page
        for (var i = 0; i < 250; i++)
        {
            AddSubscription($"s{i}", $"product-{i}", initialOrderId: "bulk-checkout");
        }

        var info = (await Service().GetForOrdersAsync(["bulk-checkout"], Customer)).Single();

        info.Subscriptions.Should().HaveCount(250);
        _subscriptionSearch.Verify(x => x.SearchAsync(It.IsAny<SubscriptionSearchCriteria>(), It.IsAny<bool>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Only_the_customers_own_subscriptions_count()
    {
        AddSubscription("s1", "pepsi", initialOrderId: "o1", customerId: "someone-else");

        var result = await Service().GetForOrdersAsync(["o1"], Customer);

        result.Should().BeEmpty();
        _subscriptionCriteria.CustomerId.Should().Be(Customer);
        _renewalCriteria.CustomerId.Should().Be(Customer);
    }

    [Fact]
    public async Task No_orders_ask_nothing_and_a_long_list_is_answered_in_batches()
    {
        (await Service().GetForOrdersAsync([])).Should().BeEmpty();
        _subscriptionSearch.Verify(x => x.SearchAsync(It.IsAny<SubscriptionSearchCriteria>(), It.IsAny<bool>()), Times.Never);

        // Every order is looked up, 100 at a time: none of a long list is left without its subscription info.
        await Service().GetForOrdersAsync(Enumerable.Range(0, 250).Select(i => $"o{i}").ToList());
        _subscriptionCriteria.InitialOrderIds.Should().HaveCount(50).And.Contain("o249");
        _renewalCriteria.OrderIds.Should().HaveCount(50).And.Contain("o249");
    }
}
