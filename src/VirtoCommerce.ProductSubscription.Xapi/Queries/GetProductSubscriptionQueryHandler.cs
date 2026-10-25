using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetProductSubscriptionQueryHandler(ISubscriptionService subscriptionService)
    : IQueryHandler<GetProductSubscriptionQuery, Subscription>
{
    public virtual async Task<Subscription> Handle(GetProductSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.GetByIdAsync(request.Id);
        if (subscription == null)
        {
            return null;
        }

        AccessGuard.EnsureOwner(request.UserId, subscription.CustomerId);
        return subscription;
    }
}
