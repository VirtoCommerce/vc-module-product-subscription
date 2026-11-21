using System;
using System.Globalization;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Data.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Services;

public class SubscriptionNotificationServiceTests
{
    [Theory]
    [InlineData("en-US", "November 20, 2026")]
    [InlineData("de-DE", "20. November 2026")]
    [InlineData("ru-RU", "20 ноября 2026")]
    [InlineData("ja-JP", "2026年11月20日")]
    public void Dates_are_long_dates_without_the_weekday(string culture, string expected)
    {
        var text = TestService.Format(new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc), CultureInfo.GetCultureInfo(culture), TimeZoneInfo.Utc);

        text.Should().StartWith(expected);
        text.Should().NotContainAny("Friday", "Freitag", "пятница", "金曜日");
    }

    [Fact]
    public void Dates_fall_on_the_store_time_zone_day()
    {
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        TestService.Format(new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc), CultureInfo.GetCultureInfo("en-US"), tokyo)
            .Should().Be("November 21, 2026");
    }

    private sealed class TestService() : SubscriptionNotificationService(null, null, null, null, null, null, null)
    {
        public static string Format(DateTime date, CultureInfo culture, TimeZoneInfo timeZone)
        {
            return new TestService().FormatDate(date, culture, timeZone);
        }
    }
}
