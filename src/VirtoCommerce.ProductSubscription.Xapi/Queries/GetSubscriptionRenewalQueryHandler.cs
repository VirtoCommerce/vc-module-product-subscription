using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionRenewalQueryHandler(IRenewalService renewalService)
    : IQueryHandler<GetSubscriptionRenewalQuery, Renewal>
{
    public virtual async Task<Renewal> Handle(GetSubscriptionRenewalQuery request, CancellationToken cancellationToken)
    {
        var renewal = await renewalService.GetByIdAsync(request.Id);
        if (renewal == null)
        {
            return null;
        }

        AccessGuard.EnsureOwner(request.UserId, renewal.CustomerId);
        return renewal;
    }
}
