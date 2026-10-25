using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSavedPaymentMethodsQueryHandler(IStoredPaymentMethodSearchService searchService)
    : IQueryHandler<GetSavedPaymentMethodsQuery, IList<StoredPaymentMethod>>
{
    public virtual async Task<IList<StoredPaymentMethod>> Handle(GetSavedPaymentMethodsQuery request, CancellationToken cancellationToken)
    {
        var criteria = AbstractTypeFactory<StoredPaymentMethodSearchCriteria>.TryCreateInstance();
        criteria.StoreId = request.StoreId;
        criteria.CustomerId = request.UserId;
        criteria.Statuses = [StoredPaymentMethodStatus.Active, StoredPaymentMethodStatus.NeedsUpdate, StoredPaymentMethodStatus.Expired];
        criteria.Take = 100;
        return (await searchService.SearchAsync(criteria)).Results;
    }
}
