using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class RemoveCartItemSubscriptionCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionCartItemService cartItemService)
    : IRequestHandler<RemoveCartItemSubscriptionCommand, bool>
{
    private const int MaxChoicesPerLine = 10;

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
        criteria.Take = MaxChoicesPerLine;
        var items = (await cartItemSearchService.SearchAsync(criteria)).Results;
        if (items.Count == 0)
        {
            return false;
        }

        await cartItemService.DeleteAsync(items.Select(x => x.Id).ToList());
        await cartService.SaveChangesAsync([cart]);
        return true;
    }
}
