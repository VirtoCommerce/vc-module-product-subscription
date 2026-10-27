using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetCartSubscriptionItemsQueryHandler(IShoppingCartService cartService, ISubscriptionCartItemSearchService searchService)
    : IQueryHandler<GetCartSubscriptionItemsQuery, IList<SubscriptionCartItem>>
{
    private const int MaxCartChoices = 1000;

    public virtual async Task<IList<SubscriptionCartItem>> Handle(GetCartSubscriptionItemsQuery request, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetNoCloneAsync(request.CartId);
        if (cart == null)
        {
            return [];
        }

        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.Take = MaxCartChoices;
        var lineIds = cart.Items?.Select(x => x.Id).ToHashSet() ?? [];
        return (await searchService.SearchAsync(criteria)).Results.Where(x => lineIds.Contains(x.LineItemId)).ToList();
    }
}
