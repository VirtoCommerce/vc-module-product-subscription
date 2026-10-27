using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Models;
using VirtoCommerce.ProductSubscription.Data.Repositories;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionPlanSearchService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    ISubscriptionPlanService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<SubscriptionPlanSearchCriteria, SubscriptionPlanSearchResult, SubscriptionPlan, SubscriptionPlanEntity>(repositoryFactory, platformMemoryCache, crudService, crudOptions),
        ISubscriptionPlanSearchService
{
    protected override IQueryable<SubscriptionPlanEntity> BuildQuery(IRepository repository, SubscriptionPlanSearchCriteria criteria)
    {
        var query = ((IProductSubscriptionRepository)repository).Plans;

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (criteria.IsActive != null)
        {
            query = query.Where(x => x.IsActive == criteria.IsActive);
        }

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            query = query.Where(x => x.Name.Contains(criteria.Keyword));
        }

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(SubscriptionPlanSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;
        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(SubscriptionPlanEntity.Priority), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(SubscriptionPlanEntity.Name) },
                new SortInfo { SortColumn = nameof(SubscriptionPlanEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
