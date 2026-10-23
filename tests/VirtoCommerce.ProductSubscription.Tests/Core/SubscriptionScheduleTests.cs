using System;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Models;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Core;

[Trait("Category", "Unit")]
public class SubscriptionScheduleTests
{
    private static readonly DateTime _start = new(2025, 3, 10, 8, 30, 0, DateTimeKind.Utc);

    // AddInterval

    [Theory]
    [InlineData(SubscriptionIntervalUnit.Day, 1, "2025-03-11")]
    [InlineData(SubscriptionIntervalUnit.Day, 30, "2025-04-09")]
    [InlineData(SubscriptionIntervalUnit.Week, 1, "2025-03-17")]
    [InlineData(SubscriptionIntervalUnit.Week, 2, "2025-03-24")]
    [InlineData(SubscriptionIntervalUnit.Month, 1, "2025-04-10")]
    [InlineData(SubscriptionIntervalUnit.Month, 3, "2025-06-10")]
    [InlineData(SubscriptionIntervalUnit.Year, 1, "2026-03-10")]
    [InlineData(SubscriptionIntervalUnit.Year, 2, "2027-03-10")]
    public void AddInterval_AddsUnitTimesCount(SubscriptionIntervalUnit unit, int count, string expectedDate)
    {
        // Act
        var result = SubscriptionSchedule.AddInterval(_start, unit, count);

        // Assert
        result.Date.Should().Be(DateTime.Parse(expectedDate));
        result.TimeOfDay.Should().Be(_start.TimeOfDay);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddInterval_NonPositiveCount_TreatedAsOne(int count)
    {
        // Act
        var result = SubscriptionSchedule.AddInterval(_start, SubscriptionIntervalUnit.Week, count);

        // Assert
        result.Should().Be(_start.AddDays(7));
    }

    [Theory]
    [InlineData("2025-01-31", 1, "2025-02-28")]
    [InlineData("2024-01-31", 1, "2024-02-29")]
    [InlineData("2025-03-31", 1, "2025-04-30")]
    [InlineData("2025-08-31", 1, "2025-09-30")]
    [InlineData("2025-01-31", 3, "2025-04-30")]
    [InlineData("2025-12-31", 2, "2026-02-28")]
    public void AddInterval_Month_ClampsToLastDayOfShorterMonth(string start, int count, string expected)
    {
        // Act
        var result = SubscriptionSchedule.AddInterval(DateTime.Parse(start), SubscriptionIntervalUnit.Month, count);

        // Assert
        result.Should().Be(DateTime.Parse(expected));
    }

    [Fact]
    public void AddInterval_Year_FromLeapDay_ClampsToFebruary28()
    {
        // Act
        var result = SubscriptionSchedule.AddInterval(new DateTime(2024, 2, 29), SubscriptionIntervalUnit.Year, 1);

        // Assert
        result.Should().Be(new DateTime(2025, 2, 28));
    }

    [Fact]
    public void AddInterval_UnknownUnit_Throws()
    {
        // Act
        var act = () => SubscriptionSchedule.AddInterval(_start, (SubscriptionIntervalUnit)42, 1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("unit");
    }

    // NextAfter

    [Fact]
    public void NextAfter_PreviousPlusIntervalInFuture_ReturnsOneIntervalLater()
    {
        // Arrange
        var previous = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2025, 3, 5, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = SubscriptionSchedule.NextAfter(previous, SubscriptionIntervalUnit.Week, 2, now);

        // Assert
        result.Should().Be(new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextAfter_LateSchedulerRun_SkipsWholeIntervalsAndKeepsTheAnchor()
    {
        // Arrange: the scheduler ran 10 days late on a weekly subscription.
        var previous = new DateTime(2025, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2025, 3, 18, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = SubscriptionSchedule.NextAfter(previous, SubscriptionIntervalUnit.Week, 1, now);

        // Assert: 03-08 and 03-15 are in the past, 03-22 is the next slot on the same weekday and time.
        result.Should().Be(new DateTime(2025, 3, 22, 9, 0, 0, DateTimeKind.Utc));
        result.DayOfWeek.Should().Be(previous.DayOfWeek);
    }

    [Fact]
    public void NextAfter_CandidateEqualToNow_IsSkipped()
    {
        // Arrange
        var previous = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = previous.AddDays(1);

        // Act
        var result = SubscriptionSchedule.NextAfter(previous, SubscriptionIntervalUnit.Day, 1, now);

        // Assert
        result.Should().Be(previous.AddDays(2));
    }

    [Fact]
    public void NextAfter_PreviousInFuture_StillAdvancesOneInterval()
    {
        // Arrange
        var previous = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = SubscriptionSchedule.NextAfter(previous, SubscriptionIntervalUnit.Month, 1, now);

        // Assert
        result.Should().Be(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextAfter_ResultIsAlwaysAfterNow()
    {
        // Arrange
        var previous = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = SubscriptionSchedule.NextAfter(previous, SubscriptionIntervalUnit.Month, 2, now);

        // Assert
        result.Should().BeAfter(now);
        result.Should().Be(new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    // ApplyDiscount / GetDiscountPerItem

    [Theory]
    [InlineData(100, 10, 90)]
    [InlineData(19.99, 15, 16.99)] // 2.9985 discount rounds to 3.00
    [InlineData(10.05, 50, 5.02)] // 5.025 discount rounds away from zero to 5.03
    [InlineData(50, 0, 50)]
    [InlineData(50, 100, 0)]
    [InlineData(50, 150, 0)] // clamped to 100 %
    [InlineData(50, -10, 50)] // clamped to 0 %
    public void ApplyDiscount_Percent(decimal price, decimal percent, decimal expected)
    {
        // Act
        var result = SubscriptionSchedule.ApplyDiscount(price, SubscriptionDiscountType.Percent, percent);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 15, 85)]
    [InlineData(9.99, 2.5, 7.49)]
    [InlineData(10, 10, 0)]
    [InlineData(10, 25, 0)] // never below zero
    [InlineData(10, -5, 10)] // negative amount gives no discount
    public void ApplyDiscount_FixedAmount(decimal price, decimal amount, decimal expected)
    {
        // Act
        var result = SubscriptionSchedule.ApplyDiscount(price, SubscriptionDiscountType.FixedAmount, amount);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SubscriptionDiscountType.Percent, 10)]
    [InlineData(SubscriptionDiscountType.FixedAmount, 10)]
    public void ApplyDiscount_ZeroPrice_StaysZero(SubscriptionDiscountType type, decimal value)
    {
        // Act
        var result = SubscriptionSchedule.ApplyDiscount(0m, type, value);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public void ApplyDiscount_None_ReturnsPrice()
    {
        // Act
        var result = SubscriptionSchedule.ApplyDiscount(42.5m, SubscriptionDiscountType.None, 30);

        // Assert
        result.Should().Be(42.5m);
    }

    [Theory]
    [InlineData(SubscriptionDiscountType.Percent, 10, 2.5)]
    [InlineData(SubscriptionDiscountType.FixedAmount, 4, 4)]
    [InlineData(SubscriptionDiscountType.FixedAmount, 40, 25)]
    [InlineData(SubscriptionDiscountType.None, 10, 0)]
    public void GetDiscountPerItem_ReturnsDiscountAmount(SubscriptionDiscountType type, decimal value, decimal expected)
    {
        // Act
        var result = SubscriptionSchedule.GetDiscountPerItem(25m, type, value);

        // Assert
        result.Should().Be(expected);
    }

    // Describe

    [Theory]
    [InlineData(SubscriptionIntervalUnit.Day, 1, "every day")]
    [InlineData(SubscriptionIntervalUnit.Week, 1, "every week")]
    [InlineData(SubscriptionIntervalUnit.Month, 1, "every month")]
    [InlineData(SubscriptionIntervalUnit.Year, 1, "every year")]
    [InlineData(SubscriptionIntervalUnit.Day, 10, "every 10 days")]
    [InlineData(SubscriptionIntervalUnit.Week, 2, "every 2 weeks")]
    [InlineData(SubscriptionIntervalUnit.Month, 3, "every 3 months")]
    [InlineData(SubscriptionIntervalUnit.Year, 2, "every 2 years")]
    public void Describe_ReturnsHumanReadableFrequency(SubscriptionIntervalUnit unit, int count, string expected)
    {
        // Act
        var result = SubscriptionSchedule.Describe(unit, count);

        // Assert
        result.Should().Be(expected);
    }
}
