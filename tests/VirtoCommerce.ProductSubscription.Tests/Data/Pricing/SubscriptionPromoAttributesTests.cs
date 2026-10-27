using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Pricing;

[Trait("Category", "Unit")]
public class SubscriptionPromoAttributesTests
{
    [Theory]
    [InlineData(SubscriptionDiscountType.Percent, 12.5)]
    [InlineData(SubscriptionDiscountType.FixedAmount, 3)]
    [InlineData(SubscriptionDiscountType.None, 0)]
    public void SetThenGet_RoundTrips(SubscriptionDiscountType type, decimal value)
    {
        // Arrange
        var entry = new ProductPromoEntry { ProductId = "P1" };
        var discount = new SubscriptionLineDiscount("plan-1", "Subscribe & Save", type, value);

        // Act
        SubscriptionPromoAttributes.Set(entry, discount);
        var result = SubscriptionPromoAttributes.Get(entry);

        // Assert
        result.Should().Be(discount);
    }

    [Fact]
    public void Set_UsesInvariantCultureForValue()
    {
        // Arrange
        var entry = new ProductPromoEntry();

        // Act
        SubscriptionPromoAttributes.Set(entry, new SubscriptionLineDiscount("p", "n", SubscriptionDiscountType.Percent, 12.5m));

        // Assert
        entry.Attributes[SubscriptionPromoAttributes.DiscountValue].Should().Be("12.5");
        entry.Attributes[SubscriptionPromoAttributes.DiscountType].Should().Be("Percent");
    }

    [Fact]
    public void Set_KeepsExistingAttributes()
    {
        // Arrange
        var entry = new ProductPromoEntry { Attributes = new Dictionary<string, string> { ["other"] = "x" } };

        // Act
        SubscriptionPromoAttributes.Set(entry, new SubscriptionLineDiscount("p", "n", SubscriptionDiscountType.Percent, 5));

        // Assert
        entry.Attributes.Should().ContainKey("other").WhoseValue.Should().Be("x");
        entry.Attributes.Should().ContainKey(SubscriptionPromoAttributes.PlanId);
    }

    [Fact]
    public void Set_NullAttributes_CreatesDictionary()
    {
        // Arrange
        var entry = new ProductPromoEntry { Attributes = null };

        // Act
        SubscriptionPromoAttributes.Set(entry, new SubscriptionLineDiscount("p", "n", SubscriptionDiscountType.FixedAmount, 1));

        // Assert
        entry.Attributes.Should().HaveCount(4);
    }

    [Fact]
    public void Get_NullEntry_ReturnsNull()
    {
        SubscriptionPromoAttributes.Get(null).Should().BeNull();
    }

    [Fact]
    public void Get_NoSubscriptionAttributes_ReturnsNull()
    {
        // Arrange
        var entry = new ProductPromoEntry { Attributes = new Dictionary<string, string> { ["other"] = "x" } };

        // Act / Assert
        SubscriptionPromoAttributes.Get(entry).Should().BeNull();
    }

    [Fact]
    public void Get_InvalidTypeAndValue_FallsBackToNoneAndZero()
    {
        // Arrange
        var entry = new ProductPromoEntry
        {
            Attributes = new Dictionary<string, string>
            {
                [SubscriptionPromoAttributes.DiscountType] = "garbage",
                [SubscriptionPromoAttributes.DiscountValue] = "not-a-number",
            },
        };

        // Act
        var result = SubscriptionPromoAttributes.Get(entry);

        // Assert
        result.Should().NotBeNull();
        result.DiscountType.Should().Be(SubscriptionDiscountType.None);
        result.DiscountValue.Should().Be(0m);
        result.PlanId.Should().BeNull();
        result.PlanName.Should().BeNull();
    }
}
