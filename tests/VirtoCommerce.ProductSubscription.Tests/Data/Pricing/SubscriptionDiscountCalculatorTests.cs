using FluentAssertions;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Pricing;

[Trait("Category", "Unit")]
public class SubscriptionDiscountCalculatorTests
{
    private static readonly Currency _currency = new(new Language("en-US"), "USD");

    private readonly SubscriptionDiscountCalculator _sut = new();

    [Theory]
    [InlineData(SubscriptionDiscountType.Percent, 10, 25, 2.5)]
    [InlineData(SubscriptionDiscountType.Percent, 15, 19.99, 2.9985)]
    [InlineData(SubscriptionDiscountType.FixedAmount, 3, 25, 3)]
    [InlineData(SubscriptionDiscountType.FixedAmount, 30, 25, 25)]
    [InlineData(SubscriptionDiscountType.None, 10, 25, 0)]
    [InlineData(SubscriptionDiscountType.Percent, 0, 25, 0)]
    [InlineData(SubscriptionDiscountType.Percent, 10, 0, 0)]
    public void GetDiscountPerItem_MatchesTheCartReward(SubscriptionDiscountType type, double value, double price, double expected)
    {
        var result = _sut.GetDiscountPerItem(type, (decimal)value, (decimal)price, _currency);

        result.Should().Be((decimal)expected);
    }

    [Fact]
    public void GetDiscountPerItem_EqualsTheRewardTheCartApplies()
    {
        var discount = new SubscriptionLineDiscount("plan-1", "Monthly", SubscriptionDiscountType.Percent, 12.5m);
        var cartReward = SubscriptionPromoEvaluatorDecorator.CreateReward("P1", discount);

        var result = _sut.GetDiscountPerItem(discount.DiscountType, discount.DiscountValue, 40m, _currency);

        result.Should().Be(cartReward.GetAmountPerItem(40m, 1, _currency));
    }
}
