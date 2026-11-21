using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Handlers;
using VirtoCommerce.ProductSubscription.Tests.Data.Renewals;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Handlers;

/// <summary>
/// The cart change right after checkout deletes the subscription choices. When creating the subscriptions during
/// checkout fails, the job that retries gets a copy of the choices, so the customer still gets the subscriptions.
/// </summary>
[Trait("Category", "Unit")]
public class OrderChoicesSnapshotTests
{
    private static readonly List<SubscriptionCartItem> _choices =
        [new SubscriptionCartItem { Id = "choice-1", CartId = "cart-1", LineItemId = "line-1", ProductId = "coffee", PlanId = "plan-1" }];

    [Fact]
    public async Task CreatingFailsAtCheckout_TheJobGetsACopyOfTheChoices()
    {
        var manager = new Mock<ISubscriptionManager>();
        manager.Setup(x => x.CreateFromOrderAsync(It.IsAny<CustomerOrder>())).ThrowsAsync(new InvalidOperationException("database is down"));
        manager.Setup(x => x.GetCartChoicesAsync("cart-1")).ReturnsAsync(_choices);
        var handler = new RecordingOrderChangedEventHandler(manager.Object);

        await handler.Handle(new OrderChangedEvent([new GenericChangedEntry<CustomerOrder>(Order(), EntryState.Added)]));

        handler.Enqueued.Should().ContainSingle().Which.Choices.Should().BeEquivalentTo(_choices);
    }

    [Fact]
    public async Task CreatingSucceedsAtCheckout_TheJobCarriesNoCopy()
    {
        var manager = new Mock<ISubscriptionManager>();
        manager.Setup(x => x.CreateFromOrderAsync(It.IsAny<CustomerOrder>())).ReturnsAsync(new List<Subscription>());
        var handler = new RecordingOrderChangedEventHandler(manager.Object);

        await handler.Handle(new OrderChangedEvent([new GenericChangedEntry<CustomerOrder>(Order(), EntryState.Added)]));

        handler.Enqueued.Should().ContainSingle().Which.Choices.Should().BeNull();
        manager.Verify(x => x.GetCartChoicesAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TheJob_CreatesTheSubscriptionsFromTheCopy()
    {
        var order = Order();
        var orders = new Mock<ICustomerOrderService>();
        orders.Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync([order]);
        var manager = new Mock<ISubscriptionManager>();
        manager.Setup(x => x.CreateFromOrderAsync(It.IsAny<CustomerOrder>(), It.IsAny<IList<SubscriptionCartItem>>())).ReturnsAsync(new List<Subscription>());
        var job = new OrderChangedJob(orders.Object, manager.Object, Mock.Of<IRenewalProcessor>(), new InProcessDistributedLock(), NullLogger<OrderChangedJob>.Instance);

        await job.Execute(new OrderChangedJobPayload { OrderId = order.Id, IsNew = true, Choices = _choices }, context: null, TestContext.Current.CancellationToken);

        manager.Verify(x => x.CreateFromOrderAsync(order, It.Is<IList<SubscriptionCartItem>>(c => c.Single().Id == "choice-1")), Times.Once);
        manager.Verify(x => x.CreateFromOrderAsync(It.IsAny<CustomerOrder>()), Times.Never);
    }

    private static CustomerOrder Order()
    {
        return new CustomerOrder
        {
            Id = "order-1",
            Number = "CO-1",
            ShoppingCartId = "cart-1",
            CustomerId = "customer-1",
            Items = [new LineItem { Id = "o-line-1", ProductId = "coffee", Quantity = 1 }],
        };
    }
}
