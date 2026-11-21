using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;
using CartAggregate = VirtoCommerce.XCart.Core.CartAggregate;
using VirtoCommerce.ProductSubscription.Core;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class RemoveCartItemSubscriptionCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionCartItemService cartItemService)
    : IRequestHandler<RemoveCartItemSubscriptionCommand, bool>
{

    public virtual async Task<bool> Handle(RemoveCartItemSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetByIdAsync(request.CartId);
        if (cart == null)
        {
            return false;
        }

        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.LineItemId = request.LineItemId;
        criteria.Take = ModuleConstants.SearchPageSize;
        var items = await cartItemSearchService.SearchAllAsync(criteria);
        if (items.Count == 0)
        {
            return false;
        }

        await cartItemService.DeleteAsync(items.Select(x => x.Id).ToList());
        await TouchCartAsync(cart);
        return true;
    }

    /// <summary>
    /// Expires xCart's cached cart, so the next cart query is recalculated with the subscription discount. The cart
    /// itself isn't saved: saving a copy loaded earlier would undo a change made to the cart in the meantime.
    /// </summary>
    protected virtual Task TouchCartAsync(ShoppingCart cart)
    {
        GenericCachingRegion<CartAggregate>.ExpireTokenForKey(cart.Id);
        return Task.CompletedTask;
    }
}
