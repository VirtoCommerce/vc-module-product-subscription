using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using Moq;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Handlers;
using VirtoCommerce.ProductSubscription.Xapi.Commands;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Xapi;

/// <summary>
/// The subscription choice of a configured cart line keeps the configuration's fingerprint, so the order line it became is
/// found by product and fingerprint. A parent of variations without its own price can't be subscribed to.
/// </summary>
[Trait("Category", "Unit")]
public class CartItemSubscriptionConfigurationTests
{
    private readonly Mock<IShoppingCartService> _carts = new();
    private readonly Mock<ISubscriptionCartItemService> _choices = new();
    private readonly Mock<ICartAggregateRepository> _aggregates = new();
    private readonly List<SubscriptionCartItem> _saved = [];

    public CartItemSubscriptionConfigurationTests()
    {
        _choices
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<SubscriptionCartItem>>()))
            .Callback((IList<SubscriptionCartItem> items) => _saved.AddRange(items))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ConfiguredLine_CanBeSubscribed_AndKeepsTheFingerprint()
    {
        var line = ConfiguredLine("fanta-mango");
        var handler = Handler(Cart(line));

        var choice = await handler.Handle(Command(line.Id), CancellationToken.None);

        choice.ConfigurationHash.Should().NotBeNullOrEmpty().And.Be(SubscriptionConfigurationMapper.GetHash(line));
        _saved.Should().ContainSingle();
        // The cart is not saved: a copy loaded earlier would undo a change made to it in the meantime.
        _carts.Verify(x => x.SaveChangesAsync(It.IsAny<IList<ShoppingCart>>()), Times.Never);
    }

    [Fact]
    public async Task ALineTheQuantityStepperAdded_IsPricedByXCart_AndCanBeSubscribed()
    {
        // N5: updateCartQuantity stores a new line before pricing it (ListPrice and SalePrice 0); xCart prices it on load
        var stored = new LineItem { Id = "line-stepper", ProductId = "gift-box", Quantity = 1, ListPrice = 0, SalePrice = 0 };
        var handler = Handler(Cart(stored));
        PricedByXCart(new LineItem { Id = "line-stepper", ProductId = "gift-box", Quantity = 1, ListPrice = 190m, SalePrice = 190m });

        var choice = await handler.Handle(Command(stored.Id), CancellationToken.None);

        choice.LineItemId.Should().Be("line-stepper");
        _saved.Should().ContainSingle();
    }

    [Fact]
    public async Task ALineWithAStoredPrice_IsNotRepricedThroughXCart()
    {
        var line = new LineItem { Id = "line-priced", ProductId = "gift-box", Quantity = 1, ListPrice = 190m, SalePrice = 190m };
        var handler = Handler(Cart(line));

        await handler.Handle(Command(line.Id), CancellationToken.None);

        _aggregates.Verify(x => x.GetCartByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ParentOfVariationsWithoutAPrice_IsRejectedWithAClearMessage()
    {
        var line = new LineItem { Id = "line-parent", ProductId = "gift-box", ListPrice = 0, SalePrice = 0 };
        var handler = Handler(Cart(line));
        // xCart has no price for it either
        PricedByXCart(new LineItem { Id = "line-parent", ProductId = "gift-box", ListPrice = 0, SalePrice = 0 });

        var act = () => handler.Handle(Command(line.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<ExecutionError>()).Which.Message.Should().Contain("variations");
        _saved.Should().BeEmpty();
    }

    [Fact]
    public async Task AnotherOneTimeLineOfTheProduct_IsRefused()
    {
        // The subscription discount reaches every line of the product: the other configuration would be discounted too.
        var line = ConfiguredLine("fanta-mango");
        var other = ConfiguredLine("cola");
        other.Id = "line-2";
        var handler = Handler(Cart(line, other));

        var act = () => handler.Handle(Command(line.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<ExecutionError>()).Which.Message.Should().Contain("more than once");
        _saved.Should().BeEmpty();
    }

    [Fact]
    public async Task AnotherLineThatIsAGiftOrNotSelected_DoesNotBlock()
    {
        var line = ConfiguredLine("fanta-mango");
        var gift = new LineItem { Id = "gift", ProductId = "gift-box", IsGift = true };
        var unselected = new LineItem { Id = "later", ProductId = "gift-box", SelectedForCheckout = false, ListPrice = 5 };
        var handler = Handler(Cart(line, gift, unselected));

        await handler.Handle(Command(line.Id), CancellationToken.None);

        _saved.Should().ContainSingle();
    }

    [Fact]
    public async Task ChangingTheOptionsInTheCart_UpdatesTheStoredFingerprint()
    {
        var line = ConfiguredLine("fanta-mango");
        var choice = new SubscriptionCartItem { Id = "choice-1", CartId = "cart-1", LineItemId = line.Id, ProductId = "gift-box", ConfigurationHash = SubscriptionConfigurationMapper.GetHash(line) };
        var search = SearchReturning(choice);
        line.ConfigurationItems.First().ProductId = "cola";

        await new CartChangedEventHandler(search.Object, _choices.Object).Handle(Changed(Cart(line)));

        _saved.Should().ContainSingle().Which.ConfigurationHash.Should().Be(SubscriptionConfigurationMapper.GetHash(line));
    }

    [Fact]
    public async Task ACartLoadedWithoutItsConfiguration_KeepsTheStoredFingerprint()
    {
        var line = ConfiguredLine("fanta-mango");
        var choice = new SubscriptionCartItem { Id = "choice-1", CartId = "cart-1", LineItemId = line.Id, ProductId = "gift-box", ConfigurationHash = SubscriptionConfigurationMapper.GetHash(line) };
        var search = SearchReturning(choice);
        line.ConfigurationItems = null;

        await new CartChangedEventHandler(search.Object, _choices.Object).Handle(Changed(Cart(line)));

        _saved.Should().BeEmpty();
    }

    private void PricedByXCart(params LineItem[] pricedLines)
    {
        var priced = new ShoppingCart { Id = "cart-1", Items = pricedLines.ToList() };
        _aggregates.Setup(x => x.GetCartByIdAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new PricedCartAggregate(priced));
    }

    /// <summary>The handler only reads <see cref="CartAggregate.Cart"/> of the priced cart.</summary>
    private sealed class PricedCartAggregate : CartAggregate
    {
        public PricedCartAggregate(ShoppingCart cart)
            : base(null, null, null, null, null, null, null, null, null, null, null, null)
        {
            Cart = cart;
        }
    }

    private SetCartItemSubscriptionCommandHandler Handler(ShoppingCart cart)
    {
        _carts.Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync([cart]);
        var offers = new Mock<ISubscriptionOfferService>();
        offers
            .Setup(x => x.GetOffersAsync(It.IsAny<string>(), It.IsAny<IList<string>>()))
            .ReturnsAsync(new Dictionary<string, SubscriptionOffer>
            {
                ["gift-box"] = new() { ProductId = "gift-box", PlanId = "plan-1", Intervals = [new SubscriptionPlanInterval { Unit = SubscriptionIntervalUnit.Month, Count = 1 }] },
            });

        return new SetCartItemSubscriptionCommandHandler(_carts.Object, offers.Object, SearchReturning().Object, _choices.Object, _aggregates.Object);
    }

    private static Mock<ISubscriptionCartItemSearchService> SearchReturning(params SubscriptionCartItem[] items)
    {
        var search = new Mock<ISubscriptionCartItemSearchService>();
        search
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionCartItemSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((SubscriptionCartItemSearchCriteria criteria, bool _) => new SubscriptionCartItemSearchResult
            {
                TotalCount = items.Length,
                Results = criteria.Skip == 0 ? items.Select(x => (SubscriptionCartItem)x.Clone()).ToList() : [],
            });
        return search;
    }

    private static SetCartItemSubscriptionCommand Command(string lineItemId)
    {
        return new SetCartItemSubscriptionCommand
        {
            CartId = "cart-1",
            LineItemId = lineItemId,
            PlanId = "plan-1",
            IntervalUnit = SubscriptionIntervalUnit.Month,
            IntervalCount = 1,
            UserId = "customer-1",
        };
    }

    private static ShoppingCart Cart(params LineItem[] lines)
    {
        return new ShoppingCart { Id = "cart-1", StoreId = "store-1", CustomerId = "customer-1", Items = lines.ToList() };
    }

    private static CartChangedEvent Changed(ShoppingCart cart)
    {
        return new CartChangedEvent([new GenericChangedEntry<ShoppingCart>(cart, cart, EntryState.Modified)]);
    }

    private static LineItem ConfiguredLine(string drinkId)
    {
        return new LineItem
        {
            Id = "line-1",
            ProductId = "gift-box",
            IsConfigured = true,
            ListPrice = 24.60m,
            ConfigurationItems =
            [
                new ConfigurationItem { SectionId = "sec-drinks", Type = "Product", ProductId = drinkId, Quantity = 6 },
                new ConfigurationItem { SectionId = "sec-card", Type = "Text", CustomText = "Thank you!" },
            ],
        };
    }
}
