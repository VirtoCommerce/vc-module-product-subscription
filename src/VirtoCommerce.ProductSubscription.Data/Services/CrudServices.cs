using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Models;
using VirtoCommerce.ProductSubscription.Data.Repositories;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionPlanService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher)
    : CrudService<SubscriptionPlan, SubscriptionPlanEntity, SubscriptionPlanChangingEvent, SubscriptionPlanChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
        ISubscriptionPlanService
{
    protected override Task<IList<SubscriptionPlanEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IProductSubscriptionRepository)repository).GetPlansByIdsAsync(ids, responseGroup);
    }
}

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

public class SubscriptionService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher,
    IUniqueNumberGenerator uniqueNumberGenerator,
    ISettingsManager settingsManager)
    : CrudService<Subscription, SubscriptionEntity, SubscriptionChangingEvent, SubscriptionChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
        ISubscriptionService
{
    protected override Task<IList<SubscriptionEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IProductSubscriptionRepository)repository).GetSubscriptionsByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<Subscription> models)
    {
        var withoutNumber = models.Where(x => string.IsNullOrEmpty(x.Number)).ToList();
        if (withoutNumber.Count > 0)
        {
            var template = await settingsManager.GetValueAsync<string>(ModuleConstants.Settings.General.SubscriptionNumberTemplate);
            foreach (var model in withoutNumber)
            {
                model.Number = uniqueNumberGenerator.GenerateNumber(template);
            }
        }

        await base.BeforeSaveChanges(models);
    }
}

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

public class RenewalService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher,
    IUniqueNumberGenerator uniqueNumberGenerator,
    ISettingsManager settingsManager)
    : CrudService<Renewal, RenewalEntity, RenewalChangingEvent, RenewalChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
        IRenewalService
{
    protected override Task<IList<RenewalEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IProductSubscriptionRepository)repository).GetRenewalsByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<Renewal> models)
    {
        var withoutNumber = models.Where(x => string.IsNullOrEmpty(x.Number)).ToList();
        if (withoutNumber.Count > 0)
        {
            var template = await settingsManager.GetValueAsync<string>(ModuleConstants.Settings.General.RenewalNumberTemplate);
            foreach (var model in withoutNumber)
            {
                model.Number = uniqueNumberGenerator.GenerateNumber(template);
            }
        }

        foreach (var model in models.Where(x => string.IsNullOrEmpty(x.IdempotencyKey)))
        {
            model.IdempotencyKey = Guid.NewGuid().ToString("N");
        }

        await base.BeforeSaveChanges(models);
    }
}

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

public class SubscriptionCartItemService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher)
    : CrudService<SubscriptionCartItem, SubscriptionCartItemEntity, SubscriptionCartItemChangingEvent, SubscriptionCartItemChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
        ISubscriptionCartItemService
{
    protected override Task<IList<SubscriptionCartItemEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IProductSubscriptionRepository)repository).GetCartItemsByIdsAsync(ids, responseGroup);
    }
}

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
