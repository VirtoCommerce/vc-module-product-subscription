using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionTermsQueryHandler(ISubscriptionTermsService termsService)
    : IQueryHandler<GetSubscriptionTermsQuery, SubscriptionTerms>
{
    public virtual Task<SubscriptionTerms> Handle(GetSubscriptionTermsQuery request, CancellationToken cancellationToken)
    {
        return termsService.GetTermsAsync(request.StoreId, request.CultureName);
    }
}
