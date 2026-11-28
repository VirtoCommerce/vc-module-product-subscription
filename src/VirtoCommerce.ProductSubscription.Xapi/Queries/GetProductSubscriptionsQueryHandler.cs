using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetProductSubscriptionsQueryHandler(ISubscriptionSearchService searchService)
    : IQueryHandler<GetProductSubscriptionsQuery, SubscriptionSearchResult>
{
    public virtual Task<SubscriptionSearchResult> Handle(GetProductSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        var criteria = request.GetSearchCriteria<SubscriptionSearchCriteria>();
        criteria.StoreId = request.StoreId;
        criteria.CustomerId = request.UserId;
        criteria.Statuses = request.Statuses?.Where(x => !string.IsNullOrEmpty(x)).ToList();
        return searchService.SearchAsync(criteria);
    }
}
