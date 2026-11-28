using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Data.Handlers;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Handlers;

[Trait("Category", "Unit")]
public class OrderChangedEventHandlerTests
{
    [Fact]
    public async Task NewCheckoutOrder_EnqueuesTheOrderChangedJob()
    {
        var handler = new RecordingOrderChangedEventHandler();

        await handler.Handle(new OrderChangedEvent([new GenericChangedEntry<CustomerOrder>(Order(PaymentStatus.New), EntryState.Added)]));

        handler.Enqueued.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new OrderChangedJobPayload { OrderId = "order-1", IsNew = true });
    }

    [Fact]
    public async Task PaymentStatusChange_EnqueuesTheOrderChangedJob()
    {
        var handler = new RecordingOrderChangedEventHandler();

        await handler.Handle(Modified(Order(PaymentStatus.Authorized), Order(PaymentStatus.New)));

        handler.Enqueued.Should().ContainSingle().Which.IsNew.Should().BeFalse();
    }

    [Fact]
    public async Task ShipmentStatusChange_EnqueuesTheOrderChangedJob()
    {
        var handler = new RecordingOrderChangedEventHandler();
        var shipped = Order(PaymentStatus.Authorized);
        shipped.Shipments = [new Shipment { Id = "shipment-1", Status = "Sent" }];
        var packed = Order(PaymentStatus.Authorized);
        packed.Shipments = [new Shipment { Id = "shipment-1", Status = "New" }];

        await handler.Handle(Modified(shipped, packed));

        handler.Enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task ChangeThatCannotMatterToSubscriptions_EnqueuesNothing()
    {
        // E.g. a comment or an address edit: it used to cost a background job and 3-4 queries for every order.
        var handler = new RecordingOrderChangedEventHandler();
        var edited = Order(PaymentStatus.Authorized);
        edited.Comment = "Leave at the door";

        await handler.Handle(Modified(edited, Order(PaymentStatus.Authorized)));

        handler.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task NewOrderWithoutACart_EnqueuesNothing()
    {
        var handler = new RecordingOrderChangedEventHandler();
        var order = Order(PaymentStatus.New);
        order.ShoppingCartId = null;

        await handler.Handle(new OrderChangedEvent([new GenericChangedEntry<CustomerOrder>(order, EntryState.Added)]));

        handler.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheJobCannotBeEnqueued_TheOrderSaveDoesNotFail()
    {
        var handler = new RecordingOrderChangedEventHandler { Fail = true };

        var handle = () => handler.Handle(Modified(Order(PaymentStatus.Paid), Order(PaymentStatus.Authorized)));

        await handle.Should().NotThrowAsync();
    }

    private static OrderChangedEvent Modified(CustomerOrder current, CustomerOrder previous)
    {
        return new OrderChangedEvent([new GenericChangedEntry<CustomerOrder>(current, previous, EntryState.Modified)]);
    }

    private static CustomerOrder Order(PaymentStatus paymentStatus) => new()
    {
        Id = "order-1",
        Number = "CO-1",
        ShoppingCartId = "cart-1",
        Status = "New",
        InPayments = [new PaymentIn { Id = "payment-1", PaymentStatus = paymentStatus, Status = paymentStatus.ToString() }],
        Shipments = [],
    };
}
