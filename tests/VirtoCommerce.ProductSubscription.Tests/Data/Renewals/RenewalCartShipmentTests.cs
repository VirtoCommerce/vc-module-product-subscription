using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// The renewal cart is never saved, and xCart prices a shipment from its rate only for a saved cart. A paid shipping method
/// must still reach the cart with its rate as the price, or xCart rejects it as "The shipment method price is changed".
/// </summary>
[Trait("Category", "Unit")]
public class RenewalCartShipmentTests
{
    [Fact]
    public async Task PaidShippingMethod_ReachesTheCartPricedAtItsRate()
    {
        var ground = new ShippingRate { Rate = 10m, DiscountAmount = 1m, OptionName = "Ground", ShippingMethod = Method("FixedRate") };
        var pickup = new ShippingRate { Rate = 0m, OptionName = "Pickup", ShippingMethod = Method("BuyOnlinePickupInStore") };
        var availMethods = new Mock<ICartAvailMethodsService>();
        availMethods.Setup(x => x.GetAvailableShippingRatesAsync(It.IsAny<CartAggregate>())).ReturnsAsync([pickup, ground]);
        var builder = new ShipmentBuilder(availMethods.Object);
        var aggregate = new CapturingCartAggregate();

        await builder.AddShipment(aggregate, new Renewal { ShippingMethodCode = "FixedRate", ShippingMethodOption = "Ground" });

        var shipment = aggregate.Shipments.Should().ContainSingle().Subject;
        shipment.ShipmentMethodCode.Should().Be("FixedRate");
        shipment.Price.Should().Be(10m);
        shipment.DiscountAmount.Should().Be(1m);
    }

    private static ShippingMethod Method(string code) => new Mock<ShippingMethod>(code) { CallBase = true }.Object;

    private sealed class ShipmentBuilder(ICartAvailMethodsService availMethods)
        : RenewalCartBuilder(Mock.Of<ICartAggregateRepository>(), availMethods, Mock.Of<ICustomerOrderBuilder>())
    {
        public Task AddShipment(CartAggregate aggregate, Renewal renewal) => AddShipmentAsync(aggregate, renewal, deliveryAddress: null);
    }

    /// <summary>Records the shipment as it is handed to xCart, with the price it carries at that moment.</summary>
    private sealed class CapturingCartAggregate : CartAggregate
    {
        public CapturingCartAggregate()
            : base(null, null, null, null, null, null, null, null, null, null, null, null)
        {
            Cart = new ShoppingCart { Currency = "USD", Shipments = new List<Shipment>() };
        }

        public List<Shipment> Shipments { get; } = [];

        public override Task<CartAggregate> AddShipmentAsync(Shipment shipment, IEnumerable<ShippingRate> availRates)
        {
            Shipments.Add(new Shipment
            {
                ShipmentMethodCode = shipment.ShipmentMethodCode,
                Price = shipment.Price,
                DiscountAmount = shipment.DiscountAmount,
            });
            return Task.FromResult<CartAggregate>(this);
        }
    }
}
