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

public class RenewalSearchService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IRenewalService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<RenewalSearchCriteria, RenewalSearchResult, Renewal, RenewalEntity>(repositoryFactory, platformMemoryCache, crudService, crudOptions),
        IRenewalSearchService
{
    protected override IQueryable<RenewalEntity> BuildQuery(IRepository repository, RenewalSearchCriteria criteria)
    {
        var repo = (IProductSubscriptionRepository)repository;
        var query = repo.Renewals;

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (!string.IsNullOrEmpty(criteria.CustomerId))
        {
            query = query.Where(x => x.CustomerId == criteria.CustomerId);
        }

        if (!criteria.States.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.States.Contains(x.State));
        }

        if (criteria.OnlyOpen == true)
        {
            var finalStates = RenewalState.Final;
            query = query.Where(x => !finalStates.Contains(x.State));
        }

        if (!string.IsNullOrEmpty(criteria.OrderId))
        {
            query = query.Where(x => x.OrderId == criteria.OrderId);
        }

        if (!string.IsNullOrEmpty(criteria.SubscriptionId))
        {
            query = query.Where(x => repo.RenewalItems.Any(i => i.RenewalId == x.Id && i.SubscriptionId == criteria.SubscriptionId));
        }

        if (criteria.DueBefore != null)
        {
            query = query.Where(x => x.NextProcessingDate != null && x.NextProcessingDate <= criteria.DueBefore);
        }

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            query = query.Where(x => x.Number.Contains(criteria.Keyword)
                || x.OrderNumber.Contains(criteria.Keyword)
                || x.CustomerName.Contains(criteria.Keyword));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(RenewalSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;
        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(RenewalEntity.ScheduledDate), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(RenewalEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
