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

public class SubscriptionCartItemSearchService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    ISubscriptionCartItemService crudService,
    IOptions<CrudOptions> crudOptions)
    : SearchService<SubscriptionCartItemSearchCriteria, SubscriptionCartItemSearchResult, SubscriptionCartItem, SubscriptionCartItemEntity>(repositoryFactory, platformMemoryCache, crudService, crudOptions),
        ISubscriptionCartItemSearchService
{
    protected override IQueryable<SubscriptionCartItemEntity> BuildQuery(IRepository repository, SubscriptionCartItemSearchCriteria criteria)
    {
        var query = ((IProductSubscriptionRepository)repository).CartItems;

        if (!criteria.CartIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.CartIds.Contains(x.CartId));
        }

        if (!string.IsNullOrEmpty(criteria.LineItemId))
        {
            query = query.Where(x => x.LineItemId == criteria.LineItemId);
        }

        if (!string.IsNullOrEmpty(criteria.ProductId))
        {
            query = query.Where(x => x.ProductId == criteria.ProductId);
        }

        return query;
    }

    protected override IList<SortInfo> BuildSortExpression(SubscriptionCartItemSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;
        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos = [new SortInfo { SortColumn = nameof(SubscriptionCartItemEntity.CreatedDate) }, new SortInfo { SortColumn = nameof(SubscriptionCartItemEntity.Id) }];
        }

        return sortInfos;
    }
}
