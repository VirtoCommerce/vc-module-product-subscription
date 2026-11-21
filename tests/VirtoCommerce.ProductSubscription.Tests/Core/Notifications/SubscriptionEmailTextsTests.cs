using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Core.Notifications;

public class SubscriptionEmailTextsTests
{
    [Theory]
    [InlineData("en-US", SubscriptionIntervalUnit.Month, 1, "Every month")]
    [InlineData("en-US", SubscriptionIntervalUnit.Week, 2, "Every 2 weeks")]
    [InlineData("de-DE", SubscriptionIntervalUnit.Month, 3, "Alle 3 Monate")]
    [InlineData("ru-RU", SubscriptionIntervalUnit.Month, 1, "Раз в 1 месяц")]
    [InlineData("ru-RU", SubscriptionIntervalUnit.Month, 3, "Раз в 3 месяца")]
    [InlineData("ru-RU", SubscriptionIntervalUnit.Day, 21, "Раз в 21 день")]
    [InlineData("ru-RU", SubscriptionIntervalUnit.Week, 5, "Раз в 5 недель")]
    [InlineData("pl-PL", SubscriptionIntervalUnit.Month, 1, "Co miesiąc")]
    [InlineData("pl-PL", SubscriptionIntervalUnit.Week, 22, "Co 22 tygodnie")]
    [InlineData("pl-PL", SubscriptionIntervalUnit.Year, 12, "Co 12 lat")]
    [InlineData("nb-NO", SubscriptionIntervalUnit.Month, 1, "Hver måned")]
    [InlineData("de_DE", SubscriptionIntervalUnit.Month, 3, "Alle 3 Monate")]
    [InlineData("ru-RU", SubscriptionIntervalUnit.Day, 11, "Раз в 11 дней")]
    [InlineData("pl-PL", SubscriptionIntervalUnit.Week, 21, "Co 21 tygodni")]
    [InlineData("xx-XX", SubscriptionIntervalUnit.Day, 1, "Every day")]
    public void Frequency_is_in_the_email_language(string language, SubscriptionIntervalUnit unit, int count, string expected)
    {
        SubscriptionEmailTexts.Frequency(language, unit, count).Should().Be(expected);
    }

    [Fact]
    public void Discount_uses_the_percent_or_the_amount_text()
    {
        SubscriptionEmailTexts.Discount("en-US", SubscriptionDiscountType.Percent, "10").Should().Be("Save 10%");
        SubscriptionEmailTexts.Discount("de-DE", SubscriptionDiscountType.FixedAmount, "5,00 €").Should().Be("5,00 € sparen");
    }
}
