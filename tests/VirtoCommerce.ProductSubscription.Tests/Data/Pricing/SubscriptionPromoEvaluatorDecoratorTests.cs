using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Pricing;

[Trait("Category", "Unit")]
public class SubscriptionPromoEvaluatorDecoratorTests
{
    private readonly Mock<IMarketingPromoEvaluator> _innerMock = new();

    private SubscriptionPromoEvaluatorDecorator CreateSut() => new(_innerMock.Object);

    private static ProductPromoEntry CreateEntry(string productId, SubscriptionLineDiscount discount = null)
    {
        var entry = new ProductPromoEntry { ProductId = productId, Quantity = 1, Price = 10m };
        if (discount != null)
        {
            SubscriptionPromoAttributes.Set(entry, discount);
        }

        return entry;
    }

    private static PromotionEvaluationContext CreateContext(params ProductPromoEntry[] entries)
    {
        return new PromotionEvaluationContext { PromoEntries = entries.ToList() };
    }

    private static CatalogItemAmountReward[] GetSubscriptionRewards(PromotionResult result)
    {
        return result.Rewards
            .OfType<CatalogItemAmountReward>()
            .Where(x => x.PromotionId?.StartsWith(SubscriptionPromoEvaluatorDecorator.PromotionIdPrefix) == true)
            .ToArray();
    }

    [Fact]
    public async Task EvaluatePromotionAsync_InnerReturnsSameCachedInstance_DoesNotAccumulateRewards()
    {
        // Arrange: the Marketing evaluation policy caches its result and returns the same instance every time.
        var regularReward = new CartSubtotalReward { IsValid = true, PromotionId = "regular-promo", Amount = 5m };
        var cachedResult = new PromotionResult();
        cachedResult.Rewards.Add(regularReward);
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(cachedResult);

        var discount = new SubscriptionLineDiscount("plan-1", "Monthly", SubscriptionDiscountType.Percent, 10m);
        var context = CreateContext(CreateEntry("P1", discount), CreateEntry("P2", discount));
        var sut = CreateSut();

        for (var i = 0; i < 3; i++)
        {
            // Act
            var result = await sut.EvaluatePromotionAsync(context);

            // Assert
            var subscriptionRewards = GetSubscriptionRewards(result);
            subscriptionRewards.Should().HaveCount(2, "exactly one subscription reward per marked product (call {0})", i + 1);
            subscriptionRewards.Select(x => x.ProductId).Should().BeEquivalentTo(["P1", "P2"]);
            result.Rewards.Should().Contain(x => ReferenceEquals(x, regularReward));
            result.Rewards.Should().HaveCount(3);

            cachedResult.Rewards.Should().ContainSingle("the inner (cached) result must never be changed")
                .Which.Should().BeSameAs(regularReward);
        }
    }

    [Fact]
    public async Task EvaluatePromotionAsync_RepeatedCallsWithFreshInnerResults_ReturnOneRewardPerMarkedProduct()
    {
        // Arrange
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(() => new PromotionResult());

        var discount = new SubscriptionLineDiscount("plan-1", "Monthly", SubscriptionDiscountType.Percent, 10m);
        var context = CreateContext(CreateEntry("P1", discount), CreateEntry("P2", discount), CreateEntry("P3"));
        var sut = CreateSut();

        for (var i = 0; i < 3; i++)
        {
            // Act
            var result = await sut.EvaluatePromotionAsync(context);

            // Assert
            var rewards = GetSubscriptionRewards(result);
            rewards.Should().HaveCount(2);
            rewards.Select(x => x.ProductId).Should().BeEquivalentTo(["P1", "P2"]);
        }
    }

    [Fact]
    public async Task EvaluatePromotionAsync_PreservesNonSubscriptionRewards()
    {
        // Arrange
        var lineReward = new CatalogItemAmountReward { IsValid = true, PromotionId = "regular-1", ProductId = "P9", Amount = 1m };
        var cartReward = new CartSubtotalReward { IsValid = true, PromotionId = "regular-2", Amount = 2m };
        var innerResult = new PromotionResult();
        innerResult.Rewards.Add(lineReward);
        innerResult.Rewards.Add(cartReward);
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(innerResult);

        var context = CreateContext(CreateEntry("P1", new SubscriptionLineDiscount("plan-1", "Monthly", SubscriptionDiscountType.FixedAmount, 2m)));

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        result.Rewards.Should().HaveCount(3);
        result.Rewards.Should().Contain(x => ReferenceEquals(x, lineReward));
        result.Rewards.Should().Contain(x => ReferenceEquals(x, cartReward));
        GetSubscriptionRewards(result).Should().ContainSingle().Which.ProductId.Should().Be("P1");
    }

    [Fact]
    public async Task EvaluatePromotionAsync_EntriesWithoutSubscriptionAttributes_AddNoReward()
    {
        // Arrange
        var innerResult = new PromotionResult();
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(innerResult);
        var context = CreateContext(CreateEntry("P1"), CreateEntry("P2"));

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        result.Should().BeSameAs(innerResult);
        result.Rewards.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SubscriptionDiscountType.None, 10)]
    [InlineData(SubscriptionDiscountType.Percent, 0)]
    [InlineData(SubscriptionDiscountType.FixedAmount, -1)]
    public async Task EvaluatePromotionAsync_NoneOrNonPositiveDiscount_AddsNoReward(SubscriptionDiscountType type, decimal value)
    {
        // Arrange
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(new PromotionResult());
        var context = CreateContext(CreateEntry("P1", new SubscriptionLineDiscount("plan-1", "Plan", type, value)));

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        result.Rewards.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SubscriptionDiscountType.Percent, RewardAmountType.Relative)]
    [InlineData(SubscriptionDiscountType.FixedAmount, RewardAmountType.Absolute)]
    public async Task EvaluatePromotionAsync_MapsDiscountTypeToRewardAmountType(SubscriptionDiscountType type, RewardAmountType expected)
    {
        // Arrange
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(new PromotionResult());
        var context = CreateContext(CreateEntry("P1", new SubscriptionLineDiscount("plan-7", "Weekly Saver", type, 15m)));

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        var reward = GetSubscriptionRewards(result).Should().ContainSingle().Subject;
        reward.AmountType.Should().Be(expected);
        reward.Amount.Should().Be(15m);
        reward.ProductId.Should().Be("P1");
        reward.IsValid.Should().BeTrue();
        reward.Quantity.Should().Be(0);
        reward.PromotionId.Should().Be("product-subscription-plan-7");
        reward.Promotion.Should().NotBeNull();
        reward.Promotion.Id.Should().Be("product-subscription-plan-7");
        reward.Promotion.Name.Should().Be("Weekly Saver");
    }

    [Fact]
    public async Task EvaluatePromotionAsync_PlanWithoutName_UsesDefaultPromotionName()
    {
        // Arrange
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(new PromotionResult());
        var context = CreateContext(CreateEntry("P1", new SubscriptionLineDiscount("plan-1", null, SubscriptionDiscountType.Percent, 5m)));

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        GetSubscriptionRewards(result).Should().ContainSingle().Which.Promotion.Name.Should().Be("Subscribe & Save");
    }

    [Fact]
    public async Task EvaluatePromotionAsync_SameProductInLineAndCartEntries_AddsSingleReward()
    {
        // Arrange
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(new PromotionResult());
        var discount = new SubscriptionLineDiscount("plan-1", "Monthly", SubscriptionDiscountType.Percent, 10m);
        var context = new PromotionEvaluationContext
        {
            PromoEntries = [CreateEntry("P1", discount)],
            CartPromoEntries = [CreateEntry("P1", discount)],
        };

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        GetSubscriptionRewards(result).Should().ContainSingle();
    }

    [Fact]
    public async Task EvaluatePromotionAsync_InnerReturnsNull_CreatesResult()
    {
        // Arrange
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync((PromotionResult)null);
        var context = CreateContext(CreateEntry("P1", new SubscriptionLineDiscount("plan-1", "Monthly", SubscriptionDiscountType.Percent, 10m)));

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        result.Should().NotBeNull();
        GetSubscriptionRewards(result).Should().ContainSingle();
    }

    [Fact]
    public async Task EvaluatePromotionAsync_NotPromotionContext_ReturnsInnerResultUnchanged()
    {
        // Arrange
        var innerResult = new PromotionResult();
        var otherContext = new Mock<IEvaluationContext>().Object;
        _innerMock.Setup(x => x.EvaluatePromotionAsync(otherContext)).ReturnsAsync(innerResult);

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(otherContext);

        // Assert
        result.Should().BeSameAs(innerResult);
        result.Rewards.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluatePromotionAsync_PassesContextToInnerEvaluator()
    {
        // Arrange
        var context = CreateContext();
        _innerMock.Setup(x => x.EvaluatePromotionAsync(context)).ReturnsAsync(new PromotionResult());

        // Act
        await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        _innerMock.Verify(x => x.EvaluatePromotionAsync(context), Times.Once);
    }

    [Fact]
    public async Task EvaluatePromotionAsync_NullEntryCollections_ReturnsInnerResult()
    {
        // Arrange
        var innerResult = new PromotionResult();
        _innerMock.Setup(x => x.EvaluatePromotionAsync(It.IsAny<IEvaluationContext>())).ReturnsAsync(innerResult);
        var context = new PromotionEvaluationContext { PromoEntries = null, CartPromoEntries = null };

        // Act
        var result = await CreateSut().EvaluatePromotionAsync(context);

        // Assert
        result.Should().BeSameAs(innerResult);
    }
}
