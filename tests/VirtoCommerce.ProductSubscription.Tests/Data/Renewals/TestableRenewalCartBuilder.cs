using System.Linq;
using Moq;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>Exposes how the cart builder splits a shared cart line between subscriptions.</summary>
public class TestableRenewalCartBuilder()
    : RenewalCartBuilder(Mock.Of<ICartAggregateRepository>(), Mock.Of<ICartAvailMethodsService>(), Mock.Of<ICustomerOrderBuilder>())
{
    public static decimal[] Split(int lineQuantity, decimal lineTotal, params int[] quantities)
    {
        var share = new LineShare(lineQuantity, lineTotal, quantities.Length);
        return quantities.Select(share.Take).ToArray();
    }
}
