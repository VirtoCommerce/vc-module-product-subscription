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

public class SubscriptionSearchService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    ISubscriptionService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<SubscriptionSearchCriteria, SubscriptionSearchResult, Subscription, SubscriptionEntity>(repositoryFactory, platformMemoryCache, crudService, crudOptions),
        ISubscriptionSearchService
{
    protected override IQueryable<SubscriptionEntity> BuildQuery(IRepository repository, SubscriptionSearchCriteria criteria)
    {
        var query = ((IProductSubscriptionRepository)repository).Subscriptions;

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

        if (!string.IsNullOrEmpty(criteria.OrganizationId))
        {
            query = query.Where(x => x.OrganizationId == criteria.OrganizationId);
        }

        if (!criteria.Statuses.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Statuses.Contains(x.Status));
        }

        if (!string.IsNullOrEmpty(criteria.ProductId))
        {
            query = query.Where(x => x.ProductId == criteria.ProductId);
        }

        if (!string.IsNullOrEmpty(criteria.PlanId))
        {
            query = query.Where(x => x.PlanId == criteria.PlanId);
        }

        if (!string.IsNullOrEmpty(criteria.InitialOrderId))
        {
            query = query.Where(x => x.InitialOrderId == criteria.InitialOrderId);
        }

        if (!string.IsNullOrEmpty(criteria.StoredPaymentMethodId))
        {
            query = query.Where(x => x.StoredPaymentMethodId == criteria.StoredPaymentMethodId);
        }

        if (!string.IsNullOrEmpty(criteria.OuterId))
        {
            query = query.Where(x => x.OuterId == criteria.OuterId);
        }

        if (!criteria.OuterIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OuterIds.Contains(x.OuterId));
        }

        if (criteria.NextOrderDateBefore != null)
        {
            query = query.Where(x => x.NextOrderDate <= criteria.NextOrderDateBefore);
        }

        if (criteria.WithoutOpenRenewal == true)
        {
            query = query.Where(x => x.CurrentRenewalId == null);
        }

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            query = query.Where(x => x.Number.Contains(criteria.Keyword)
                || x.ProductName.Contains(criteria.Keyword)
                || x.Sku.Contains(criteria.Keyword)
                || x.CustomerName.Contains(criteria.Keyword)
                || x.CustomerEmail.Contains(criteria.Keyword));
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(SubscriptionSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;
        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(SubscriptionEntity.CreatedDate), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(SubscriptionEntity.Id) },
            ];
        }

        return sortInfos;
    }
}
