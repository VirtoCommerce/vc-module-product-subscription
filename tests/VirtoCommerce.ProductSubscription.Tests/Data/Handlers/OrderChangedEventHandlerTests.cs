using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Data.Handlers;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Handlers;

[Trait("Category", "Unit")]
public class OrderChangedEventHandlerTests
{
    [Fact]
    public async Task Handle_EnqueuesTheOrderChangedJob()
    {
        var handler = new RecordingOrderChangedEventHandler();

        await handler.Handle(ChangedEvent(new CustomerOrder { Id = "order-1", Number = "CO-1" }, EntryState.Added));

        handler.Enqueued.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new OrderChangedJobPayload { OrderId = "order-1", IsNew = true });
    }

    [Fact]
    public async Task Handle_WhenTheJobCannotBeEnqueued_DoesNotFailTheOrderSave()
    {
        var handler = new RecordingOrderChangedEventHandler { Fail = true };

        var handle = () => handler.Handle(ChangedEvent(new CustomerOrder { Id = "order-1", Number = "CO-1" }, EntryState.Modified));

        await handle.Should().NotThrowAsync();
    }

    private static OrderChangedEvent ChangedEvent(CustomerOrder order, EntryState state)
    {
        return new OrderChangedEvent([new GenericChangedEntry<CustomerOrder>(order, state)]);
    }
}
