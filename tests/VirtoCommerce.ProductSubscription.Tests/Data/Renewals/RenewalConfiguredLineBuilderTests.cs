using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Configuration;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Commands;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;
using Xunit;
using CatalogSection = VirtoCommerce.CatalogModule.Core.Model.Configuration.ProductConfigurationSection;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// A renewal rebuilds the configured product from the stored configuration through xCart. It orders exactly the stored
/// options or nothing: a removed or sold-out option marks the configuration as changed.
/// </summary>
[Trait("Category", "Unit")]
public class RenewalConfiguredLineBuilderTests
{
    private static readonly Currency _usd = new(new Language("en-US"), "USD");

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICartProductService> _products = new();
    private readonly Mock<IProductConfigurationSearchService> _configurations = new();
    private readonly Dictionary<string, CartProduct> _catalog = [];
    private readonly TestCartAggregate _aggregate = new();
    private ProductConfiguration _catalogConfiguration;

    public RenewalConfiguredLineBuilderTests()
    {
        _catalogConfiguration = new ProductConfiguration
        {
            ProductId = "gift-box",
            IsActive = true,
            Sections =
            [
                new CatalogSection { Id = "sec-drinks", Type = "Product", Options = [new ProductConfigurationOption { ProductId = "fanta-mango" }, new ProductConfigurationOption { ProductId = "cola" }] },
                new CatalogSection { Id = "sec-card", Type = "Text" },
                new CatalogSection { Id = "sec-logo", Type = "File" },
            ],
        };
        AddProduct("gift-box");
        AddProduct("fanta-mango");
        AddProduct("cola");

        _configurations
            .Setup(x => x.SearchAsync(It.IsAny<ProductConfigurationSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync(() => new ProductConfigurationSearchResult
            {
                TotalCount = _catalogConfiguration == null ? 0 : 1,
                Results = _catalogConfiguration == null ? [] : [_catalogConfiguration],
            });
        _products
            .Setup(x => x.GetCartProductsByIdsAsync(It.IsAny<CartAggregate>(), It.IsAny<IList<string>>()))
            .ReturnsAsync((CartAggregate _, IList<string> ids) => ids.Where(_catalog.ContainsKey).Select(id => _catalog[id]).ToList());
        _products
            .Setup(x => x.GetCartProductsAsync(It.IsAny<CartAggregate>(), It.IsAny<IList<(string CurrencyCode, string ProductId)>>()))
            .ReturnsAsync((CartAggregate _, IList<(string CurrencyCode, string ProductId)> pairs) =>
                (IDictionary<string, CartProduct>)pairs.Where(x => _catalog.ContainsKey(x.ProductId)).ToDictionary(x => x.ProductId, x => _catalog[x.ProductId]));
        _mediator
            .Setup(x => x.Send(It.IsAny<CreateConfiguredLineItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateConfiguredLineItemCommand command, CancellationToken _) => new ExpConfigurationLineItem { Item = Rebuild(command) });
    }

    [Fact]
    public async Task SameOptions_AddTheConfiguredLine_WithTheStoredFilesAndQuantity()
    {
        var result = await Builder().AddAsync(_aggregate, new Renewal { Number = "REN-1" }, Stored(), quantity: 3);

        result.IsConfigurationChanged.Should().BeFalse();
        result.LineItem.Should().NotBeNull();
        result.LineItem.Quantity.Should().Be(3);
        result.Configuration.Sections.Should().OnlyContain(x => x.IsAvailable == true);
        // xCart drops files owned by another cart: the files of the first order are attached again.
        result.LineItem.ConfigurationItems.Single(x => x.SectionId == "sec-logo").Files.Should().ContainSingle().Which.Url.Should().Be("/files/logo.png");
        _mediator.Verify(x => x.Send(It.Is<CreateConfiguredLineItemCommand>(c =>
            c.ConfigurableProductId == "gift-box"
            && c.Quantity == 3
            && c.ConfigurationSections.Single(s => s.SectionId == "sec-drinks").Option.ProductId == "fanta-mango"
            && c.ConfigurationSections.Single(s => s.SectionId == "sec-drinks").Option.Quantity == 6
            && c.ConfigurationSections.Single(s => s.SectionId == "sec-card").CustomText == "Thank you!"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OptionRemovedFromTheProduct_IsAChangedConfiguration_AndNothingIsAdded()
    {
        _catalogConfiguration.Sections[0].Options.RemoveAt(0);

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeTrue();
        result.LineItem.Should().BeNull();
        result.Message.Should().Contain("Fanta Mango 0.33 l");
        result.Configuration.Sections.Single(x => x.SectionId == "sec-drinks").IsAvailable.Should().BeFalse();
        result.Configuration.Sections.Single(x => x.SectionId == "sec-card").IsAvailable.Should().BeTrue();
        _aggregate.Added.Should().BeEmpty();
        _mediator.Verify(x => x.Send(It.IsAny<CreateConfiguredLineItemCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OptionOutOfStockForTheDelivery_IsAChangedConfiguration()
    {
        AddProduct("fanta-mango", inStock: 10);

        // 6 cans per box × 2 boxes need 12.
        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 2);

        result.IsConfigurationChanged.Should().BeTrue();
        result.Configuration.Sections.Single(x => x.SectionId == "sec-drinks").IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task FreeOption_IsStillAvailable()
    {
        // xCart builds configured lines from free options; a price rule would stop every renewal of such a configuration.
        AddProduct("fanta-mango", inStock: 100, price: 0m);

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeFalse();
        result.LineItem.Should().NotBeNull();
    }

    [Fact]
    public async Task OptionInStockForTheDelivery_IsAvailable()
    {
        AddProduct("fanta-mango", inStock: 12);

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 2);

        result.IsConfigurationChanged.Should().BeFalse();
    }

    [Fact]
    public async Task ConfigurationWithoutOptions_IsRebuiltAsAConfiguredLine()
    {
        var configuration = new SubscriptionConfiguration { ProductId = "gift-box", Sections = [] };

        var result = await Builder().AddAsync(_aggregate, new Renewal(), configuration, quantity: 1);

        result.IsConfigurationChanged.Should().BeFalse();
        result.LineItem.Should().NotBeNull();
        result.LineItem.IsConfigured.Should().BeTrue();
        _mediator.Verify(x => x.Send(It.Is<CreateConfiguredLineItemCommand>(c => c.ConfigurationSections.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OptionNoLongerSold_IsAChangedConfiguration()
    {
        _catalog.Remove("fanta-mango");

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeTrue();
    }

    [Fact]
    public async Task SectionRemovedFromTheProduct_IsAChangedConfiguration()
    {
        _catalogConfiguration.Sections.RemoveAt(1);

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeTrue();
        result.Configuration.Sections.Single(x => x.SectionId == "sec-card").IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ConfigurationRemovedFromTheProduct_IsAChangedConfiguration()
    {
        _catalogConfiguration = null;

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeTrue();
        result.Configuration.Sections.Should().OnlyContain(x => x.IsAvailable == false);
    }

    [Fact]
    public async Task XCartRebuildingSomethingElse_IsAChangedConfiguration()
    {
        _mediator
            .Setup(x => x.Send(It.IsAny<CreateConfiguredLineItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateConfiguredLineItemCommand command, CancellationToken _) =>
            {
                var item = Rebuild(command);
                item.ConfigurationItems.Remove(item.ConfigurationItems.Single(x => x.SectionId == "sec-card"));
                return new ExpConfigurationLineItem { Item = item };
            });

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeTrue();
        _aggregate.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task XCartFailingToFindAnOption_IsAChangedConfiguration()
    {
        _mediator
            .Setup(x => x.Send(It.IsAny<CreateConfiguredLineItemCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Product with id fanta-mango not found"));

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeTrue();
    }

    [Fact]
    public async Task ConfigurableProductGone_IsAnUnavailableProduct_NotAConfigurationChange()
    {
        _catalog.Remove("gift-box");

        var result = await Builder().AddAsync(_aggregate, new Renewal(), Stored(), quantity: 1);

        result.IsConfigurationChanged.Should().BeFalse();
        result.LineItem.Should().BeNull();
    }

    private RenewalConfiguredLineBuilder Builder()
    {
        return new RenewalConfiguredLineBuilder(_mediator.Object, _products.Object, _configurations.Object, NullLogger<RenewalConfiguredLineBuilder>.Instance);
    }

    private static SubscriptionConfiguration Stored()
    {
        return SubscriptionConfigurationMapper.FromOrderLine(VirtoCommerce.ProductSubscription.Tests.Core.SubscriptionConfigurationTests.ConfiguredOrderLine(), "order-1", "CO-1");
    }

    /// <summary>What xCart's configured line container builds: one configuration item per section, files only when the cart owns them.</summary>
    private static LineItem Rebuild(CreateConfiguredLineItemCommand command)
    {
        return new LineItem
        {
            ProductId = command.ConfigurableProductId,
            IsConfigured = true,
            Quantity = command.Quantity,
            ConfigurationItems = command.ConfigurationSections.Select(x => new ConfigurationItem
            {
                SectionId = x.SectionId,
                Type = x.Type,
                ProductId = x.Option?.ProductId,
                Quantity = x.Option?.Quantity ?? 1,
                CustomText = x.CustomText,
            }).ToList(),
        };
    }

    private void AddProduct(string id, long? inStock = null, decimal price = 1.40m)
    {
        var product = new CartProduct(new CatalogProduct { Id = id, Name = id, IsActive = true, IsBuyable = true, TrackInventory = inStock != null })
        {
            Price = new ProductPrice(_usd) { ListPrice = new Money(price, _usd), SalePrice = new Money(price, _usd) },
        };

        if (inStock != null)
        {
            product.ApplyInventories([new InventoryInfo { ProductId = id, FulfillmentCenterId = "main", InStockQuantity = inStock.Value }], new Store { MainFulfillmentCenterId = "main" });
        }

        _catalog[id] = product;
    }

    /// <summary>A cart aggregate that only records the configured lines added to it.</summary>
    private sealed class TestCartAggregate : CartAggregate
    {
        public TestCartAggregate()
            : base(null, null, null, null, null, null, null, null, null, null, null, null)
        {
            Cart = new ShoppingCart { Id = null, StoreId = "store-1", CustomerId = "customer-1", Currency = "USD", LanguageCode = "en-US", Items = new List<LineItem>() };
            Currency = _usd;
        }

        public List<LineItem> Added { get; } = [];

        public override Task<LineItem> AddConfiguredItemAndReturnAsync(NewCartItem newCartItem, LineItem newConfiguredItem)
        {
            newConfiguredItem.Quantity = newCartItem.Quantity;
            Added.Add(newConfiguredItem);
            Cart.Items.Add(newConfiguredItem);
            return Task.FromResult(newConfiguredItem);
        }
    }
}
