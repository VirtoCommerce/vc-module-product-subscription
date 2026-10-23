using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Payments;

/// <summary>
/// A cart with subscription lines can only be paid with a method that can save the card and charge it later
/// (<see cref="ISupportStoredCredentialFlow"/>), so the other methods are not offered for it.
/// </summary>
public class SubscriptionCartAvailMethodsDecorator(
    ICartAvailMethodsService inner,
    ISubscriptionCartItemSearchService cartItemSearchService)
    : ICartAvailMethodsService
{
    public virtual async Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(CartAggregate cartAggregate)
    {
        var methods = await inner.GetAvailablePaymentMethodsAsync(cartAggregate);

        if (methods == null || !await HasSubscriptionLinesAsync(cartAggregate))
        {
            return methods;
        }

        return methods.Where(x => x is ISupportStoredCredentialFlow).ToList();
    }

    public virtual Task<IEnumerable<ShippingRate>> GetAvailableShippingRatesAsync(CartAggregate cartAggregate)
    {
        return inner.GetAvailableShippingRatesAsync(cartAggregate);
    }

    public virtual Task<IEnumerable<GiftItem>> GetAvailableGiftsAsync(CartAggregate cartAggregate)
    {
        return inner.GetAvailableGiftsAsync(cartAggregate);
    }

    protected virtual async Task<bool> HasSubscriptionLinesAsync(CartAggregate cartAggregate)
    {
        // A renewal cart is built server-side for a subscription.
        if (SubscriptionDiscountScope.Current is { Count: > 0 })
        {
            return true;
        }

        var cart = cartAggregate?.Cart;
        if (string.IsNullOrEmpty(cart?.Id) || cart.Items.IsNullOrEmpty())
        {
            return false;
        }

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.Take = 1000;
        var choices = (await cartItemSearchService.SearchAsync(criteria)).Results;

        var lineIds = cart.Items.Select(x => x.Id).ToHashSet();
        return choices.Any(x => lineIds.Contains(x.LineItemId));
    }
}
