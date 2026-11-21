using System;
using System.Linq;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Models;
using VirtoCommerce.ProductSubscription.Data.Repositories;
using VirtoCommerce.ProductSubscription.Data.Services;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Services;

/// <summary>Exposes the search service's query, so tests can run its filters on in-memory entities.</summary>
public class QueryableSubscriptionSearchService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    ISubscriptionService crudService,
    IOptions<CrudOptions> crudOptions)
    : SubscriptionSearchService(repositoryFactory, platformMemoryCache, crudService, crudOptions)
{
    public IQueryable<SubscriptionEntity> Query(IRepository repository, SubscriptionSearchCriteria criteria) => BuildQuery(repository, criteria);
}
