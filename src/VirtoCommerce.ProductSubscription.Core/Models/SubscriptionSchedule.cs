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
    /// Advances the next order date from its previous value (never from "now"), skipping whole intervals
    /// until it lands in the future, so a late scheduler run never shifts the schedule.
    /// </summary>
    public static DateTime NextAfter(DateTime previous, SubscriptionIntervalUnit unit, int count, DateTime utcNow)
    {
        var next = AddInterval(previous, unit, count);
        var guard = 0;
        while (next <= utcNow && guard++ < 1000)
        {
            next = AddInterval(next, unit, count);
        }

        return next;
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
