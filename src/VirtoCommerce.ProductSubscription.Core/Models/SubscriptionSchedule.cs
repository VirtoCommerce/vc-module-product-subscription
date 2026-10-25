using System;
using VirtoCommerce.CoreModule.Core.Currency;

namespace VirtoCommerce.ProductSubscription.Core.Models;

public static class SubscriptionSchedule
{
    public static DateTime AddInterval(DateTime date, SubscriptionIntervalUnit unit, int count)
    {
        var safeCount = Math.Max(1, count);

        return unit switch
        {
            SubscriptionIntervalUnit.Day => date.AddDays(safeCount),
            SubscriptionIntervalUnit.Week => date.AddDays(7 * safeCount),
            SubscriptionIntervalUnit.Month => date.AddMonths(safeCount),
            SubscriptionIntervalUnit.Year => date.AddYears(safeCount),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
        };
    }

    /// <summary>
    /// Like <see cref="AddInterval(DateTime, SubscriptionIntervalUnit, int)"/>, but monthly and yearly steps land on
    /// <paramref name="anchorDay"/> (clamped to the month's length), so Jan 31 → Feb 28 → Mar 31 instead of staying on the 28th.
    /// </summary>
    public static DateTime AddInterval(DateTime date, SubscriptionIntervalUnit unit, int count, int? anchorDay)
    {
        var next = AddInterval(date, unit, count);
        if (anchorDay is not (>= 1 and <= 31) || unit is not (SubscriptionIntervalUnit.Month or SubscriptionIntervalUnit.Year))
        {
            return next;
        }

        var day = Math.Min(anchorDay.Value, DateTime.DaysInMonth(next.Year, next.Month));
        return new DateTime(next.Year, next.Month, day, next.Hour, next.Minute, next.Second, next.Kind).AddTicks(next.Ticks % TimeSpan.TicksPerSecond);
    }

    /// <summary>
    /// Advances the next order date from its previous value (never from "now"), skipping whole intervals
    /// until it lands in the future, so a late scheduler run never shifts the schedule.
    /// </summary>
    public static DateTime NextAfter(DateTime previous, SubscriptionIntervalUnit unit, int count, DateTime utcNow)
    {
        return NextAfter(previous, unit, count, utcNow, anchorDay: null);
    }

    /// <inheritdoc cref="NextAfter(DateTime, SubscriptionIntervalUnit, int, DateTime)"/>
    public static DateTime NextAfter(DateTime previous, SubscriptionIntervalUnit unit, int count, DateTime utcNow, int? anchorDay)
    {
        var next = AddInterval(previous, unit, count, anchorDay);
        var guard = 0;
        while (next <= utcNow && guard++ < 1000)
        {
            next = AddInterval(next, unit, count, anchorDay);
        }

        return next;
    }

    /// <summary>
    /// The earliest next delivery the module sets on its own (on a resume, a frequency change or an import): far enough
    /// ahead for the reminder to go out its lead days before the order.
    /// </summary>
    public static DateTime EarliestNextOrderDate(DateTime utcNow, int reminderLeadDays)
    {
        return utcNow.AddDays(1 + Math.Max(0, reminderLeadDays));
    }

    public static decimal ApplyDiscount(decimal price, SubscriptionDiscountType discountType, decimal discountValue)
    {
        var discount = GetDiscountPerItem(price, discountType, discountValue);
        return Math.Max(0, price - discount);
    }

    public static decimal GetDiscountPerItem(decimal price, SubscriptionDiscountType discountType, decimal discountValue)
    {
        return discountType switch
        {
            SubscriptionDiscountType.Percent => Math.Round(price * Math.Clamp(discountValue, 0, 100) / 100m, 2, MidpointRounding.AwayFromZero),
            SubscriptionDiscountType.FixedAmount => Math.Min(price, Math.Max(0, discountValue)),
            _ => 0m,
        };
    }

    public static string Describe(SubscriptionIntervalUnit unit, int count)
    {
        var name = unit.ToString().ToLowerInvariant();
        return count == 1 ? $"every {name}" : $"every {count} {name}s";
    }

    public static decimal Round(decimal amount, Currency currency)
    {
        return currency?.RoundingPolicy?.RoundMoney(amount, currency) ?? Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
