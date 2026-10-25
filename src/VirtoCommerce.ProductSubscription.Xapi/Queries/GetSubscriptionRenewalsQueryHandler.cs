using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionRenewalsQueryHandler(IRenewalSearchService searchService)
    : IQueryHandler<GetSubscriptionRenewalsQuery, RenewalSearchResult>
{
    public virtual Task<RenewalSearchResult> Handle(GetSubscriptionRenewalsQuery request, CancellationToken cancellationToken)
    {
        var criteria = request.GetSearchCriteria<RenewalSearchCriteria>();
        criteria.StoreId = request.StoreId;
        criteria.CustomerId = request.UserId;
        criteria.OnlyOpen = request.OnlyOpen;
        criteria.SubscriptionId = request.SubscriptionId;
        if (string.IsNullOrEmpty(criteria.Sort))
        {
            criteria.Sort = request.OnlyOpen == true ? "ScheduledDate" : "ScheduledDate:desc";
        }

        return searchService.SearchAsync(criteria);
    }
}
