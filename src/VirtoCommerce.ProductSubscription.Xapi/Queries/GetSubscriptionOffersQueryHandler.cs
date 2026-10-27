using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionOffersQueryHandler(ISubscriptionOfferService offerService)
    : IQueryHandler<GetSubscriptionOffersQuery, IList<SubscriptionOffer>>
{
    public virtual async Task<IList<SubscriptionOffer>> Handle(GetSubscriptionOffersQuery request, CancellationToken cancellationToken)
    {
        var offers = await offerService.GetOffersAsync(request.StoreId, request.ProductIds?.Take(100).ToList());
        return offers.Values.ToList();
    }
}
