using System;
using System.Collections.Generic;
using System.Threading;

namespace VirtoCommerce.ProductSubscription.Data.Pricing;

/// <summary>
/// Ambient discounts for a server-side renewal cart (it has no saved cart id to look choices up by).
/// The renewal engine opens a scope around cart calculation; the promotion middleware reads it.
/// </summary>
public static class SubscriptionDiscountScope
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, SubscriptionLineDiscount>> _current = new();

    public static IReadOnlyDictionary<string, SubscriptionLineDiscount> Current => _current.Value;

    public static IDisposable Begin(IReadOnlyDictionary<string, SubscriptionLineDiscount> discountsByProductId)
    {
        var previous = _current.Value;
        _current.Value = discountsByProductId;
        return new Restore(previous);
    }

    private sealed class Restore(IReadOnlyDictionary<string, SubscriptionLineDiscount> previous) : IDisposable
    {
        public void Dispose()
        {
            _current.Value = previous;
        }
    }
}
