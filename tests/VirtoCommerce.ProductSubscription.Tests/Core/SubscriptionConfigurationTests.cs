using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Models;
using Xunit;
using CartConfigurationItem = VirtoCommerce.CartModule.Core.Model.ConfigurationItem;
using CartLineItem = VirtoCommerce.CartModule.Core.Model.LineItem;
using OrderConfigurationItem = VirtoCommerce.OrdersModule.Core.Model.ConfigurationItem;
using OrderConfigurationItemFile = VirtoCommerce.OrdersModule.Core.Model.ConfigurationItemFile;
using OrderLineItem = VirtoCommerce.OrdersModule.Core.Model.LineItem;

namespace VirtoCommerce.ProductSubscription.Tests.Core;

/// <summary>
/// A configurable product is subscribed to with the options of the first order. The fingerprint identifies those options,
/// so the order line, the cart choice and renewal lines are matched by product and fingerprint.
/// </summary>
[Trait("Category", "Unit")]
public class SubscriptionConfigurationTests
{
    [Fact]
    public void Hash_IgnoresNamesPricesAndSectionOrder()
    {
        var first = new SubscriptionConfiguration { Sections = [Drinks(), Card("Thank you!")] };
        var renamed = new SubscriptionConfiguration { Sections = [Card("Thank you!"), Drinks()] };
        renamed.Sections[1].Name = "Beverages";
        renamed.Sections[1].ProductName = "Fanta Mango (new name)";
        renamed.Sections[1].UnitPrice = 99m;

        renamed.ComputeHash().Should().Be(first.ComputeHash());
        first.ComputeHash().Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void Hash_ChangesWithTheChoices()
    {
        var baseline = new SubscriptionConfiguration { Sections = [Drinks(), Card("Thank you!"), Logo("/files/logo.png")] }.ComputeHash();

        Hash(Drinks(productId: "cola"), Card("Thank you!"), Logo("/files/logo.png")).Should().NotBe(baseline);
        Hash(Drinks(quantity: 12), Card("Thank you!"), Logo("/files/logo.png")).Should().NotBe(baseline);
        Hash(Drinks(), Card("Thanks!"), Logo("/files/logo.png")).Should().NotBe(baseline);
        Hash(Drinks(), Card("Thank you!"), Logo("/files/other.png")).Should().NotBe(baseline);
        Hash(Drinks(), Card("Thank you!")).Should().NotBe(baseline);
    }

    [Fact]
    public void Hash_SeparatorsInsideATextDontMakeTwoConfigurationsEqual()
    {
        Hash(Card("a|b")).Should().NotBe(Hash(new SubscriptionConfigurationSection { SectionId = "sec-card|a", Type = "Text", CustomText = "b" }));
    }

    [Fact]
    public void Hash_IsNullWithoutSections()
    {
        new SubscriptionConfiguration().ComputeHash().Should().BeNull();
    }

    [Fact]
    public void FromOrderLine_CopiesTheConfigurationAndWhereItCameFrom()
    {
        var line = ConfiguredOrderLine();

        var configuration = SubscriptionConfigurationMapper.FromOrderLine(line, "order-1", "CO-1");

        configuration.SchemaVersion.Should().Be(SubscriptionConfiguration.CurrentSchemaVersion);
        configuration.ProductId.Should().Be("gift-box");
        configuration.CapturedFrom.Should().BeEquivalentTo(new SubscriptionConfigurationSource { OrderId = "order-1", OrderNumber = "CO-1", LineItemId = "line-1" });
        configuration.ConfiguredUnitPrice.Should().Be(24.60m);
        configuration.Sections.Should().HaveCount(3);
        configuration.Sections[0].Should().BeEquivalentTo(new SubscriptionConfigurationSection
        {
            SectionId = "sec-drinks",
            Name = "Drinks",
            Type = "Product",
            ProductId = "fanta-mango",
            Sku = "0003420",
            ProductName = "Fanta Mango 0.33 l",
            Quantity = 6,
            UnitPrice = 1.40m,
        });
        configuration.Sections[1].CustomText.Should().Be("Thank you!");
        configuration.Sections[1].ProductId.Should().BeNull();
        configuration.Sections[2].Files.Should().ContainSingle().Which.Url.Should().Be("/files/logo.png");
    }

    [Fact]
    public void FromOrderLine_ConfiguredWithoutOptions_KeepsAnEmptyConfiguration()
    {
        // Every section optional, none chosen: still a configured product, rebuilt as one at every renewal.
        var line = new OrderLineItem { Id = "line-1", ProductId = "gift-box", IsConfigured = true, ConfigurationItems = [] };

        var configuration = SubscriptionConfigurationMapper.FromOrderLine(line, "order-1", "CO-1");

        configuration.Should().NotBeNull();
        configuration.Sections.Should().BeEmpty();
        configuration.ComputeHash().Should().BeNull();
    }

    [Fact]
    public void FromOrderLine_IsNullForAProductThatIsNotConfigured()
    {
        SubscriptionConfigurationMapper.FromOrderLine(new OrderLineItem { ProductId = "coffee" }).Should().BeNull();
        SubscriptionConfigurationMapper.GetHash(new OrderLineItem { ProductId = "coffee" }).Should().BeNull();
    }

    [Fact]
    public void CartAndOrderOfTheSameChoices_HaveTheSameFingerprint_AndUnselectedOptionsAreLeftOut()
    {
        var cartLine = new CartLineItem
        {
            Id = "cart-line-1",
            ProductId = "gift-box",
            IsConfigured = true,
            ConfigurationItems =
            [
                new CartConfigurationItem { SectionId = "sec-drinks", SectionName = "Drinks", Type = "Product", ProductId = "fanta-mango", Quantity = 6, Name = "Fanta" },
                new CartConfigurationItem { SectionId = "sec-card", Type = "Text", CustomText = "Thank you!" },
                new CartConfigurationItem
                {
                    SectionId = "sec-logo", Type = "File",
                    Files = [new VirtoCommerce.CartModule.Core.Model.ConfigurationItemFile { Name = "logo.png", Url = "/files/logo.png" }],
                },
                new CartConfigurationItem { SectionId = "sec-snack", Type = "Product", ProductId = "pretzel", Quantity = 2, SelectedForCheckout = false },
            ],
        };

        SubscriptionConfigurationMapper.GetHash(cartLine).Should().Be(SubscriptionConfigurationMapper.GetHash(ConfiguredOrderLine()));
    }

    [Fact]
    public void Json_KeepsTheDocumentAndUsesCamelCase()
    {
        var configuration = SubscriptionConfigurationMapper.FromOrderLine(ConfiguredOrderLine(), "order-1", "CO-1");

        var json = SubscriptionConfigurationJson.Serialize(configuration);
        var restored = SubscriptionConfigurationJson.Deserialize(json);

        json.Should().Contain("\"schemaVersion\":1").And.Contain("\"sectionId\":\"sec-drinks\"").And.NotContain("\"isAvailable\"");
        restored.Should().BeEquivalentTo(configuration);
        restored.ComputeHash().Should().Be(configuration.ComputeHash());
        SubscriptionConfigurationJson.Deserialize(null).Should().BeNull();
        SubscriptionConfigurationJson.Serialize(null).Should().BeNull();
    }

    [Fact]
    public void Clone_CopiesTheSections()
    {
        var subscription = new Subscription { Configuration = new SubscriptionConfiguration { Sections = [Drinks()] } };

        var copy = (Subscription)subscription.Clone();
        copy.Configuration.Sections[0].Quantity = 1;

        subscription.Configuration.Sections[0].Quantity.Should().Be(6);
    }

    internal static OrderLineItem ConfiguredOrderLine(string id = "line-1", string drinkId = "fanta-mango")
    {
        return new OrderLineItem
        {
            Id = id,
            ProductId = "gift-box",
            Name = "Office box",
            IsConfigured = true,
            Quantity = 1,
            PlacedPrice = 24.60m,
            ConfigurationItems = new List<OrderConfigurationItem>
            {
                new() { SectionId = "sec-drinks", SectionName = "Drinks", Type = "Product", ProductId = drinkId, Sku = "0003420", Name = "Fanta Mango 0.33 l", Quantity = 6, SalePrice = 1.40m },
                new() { SectionId = "sec-card", SectionName = "Card", Type = "Text", CustomText = "Thank you!" },
                new()
                {
                    SectionId = "sec-logo", SectionName = "Logo", Type = "File",
                    Files = [new OrderConfigurationItemFile { Name = "logo.png", Url = "/files/logo.png", ContentType = "image/png", Size = 1024 }],
                },
            },
        };
    }

    private static string Hash(params SubscriptionConfigurationSection[] sections)
    {
        return new SubscriptionConfiguration { Sections = sections }.ComputeHash();
    }

    private static SubscriptionConfigurationSection Drinks(string productId = "fanta-mango", int quantity = 6)
    {
        return new SubscriptionConfigurationSection { SectionId = "sec-drinks", Name = "Drinks", Type = "Product", ProductId = productId, ProductName = "Fanta Mango", Quantity = quantity, UnitPrice = 1.40m };
    }

    private static SubscriptionConfigurationSection Card(string text)
    {
        return new SubscriptionConfigurationSection { SectionId = "sec-card", Name = "Card", Type = "Text", CustomText = text };
    }

    private static SubscriptionConfigurationSection Logo(string url)
    {
        return new SubscriptionConfigurationSection { SectionId = "sec-logo", Name = "Logo", Type = "File", Files = [new SubscriptionConfigurationFile { Name = "logo.png", Url = url }] };
    }
}
