using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
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

    protected override Task BeforeSaveChanges(IList<SubscriptionPlan> models)
    {
        foreach (var plan in models)
        {
            Validate(plan);
        }

        return base.BeforeSaveChanges(models);
    }

    /// <summary>
    /// Rejects a plan customers couldn't subscribe with (a <see cref="SubscriptionOperationException"/> with the reason),
    /// and makes exactly one frequency the default: the first one, unless one is already marked.
    /// </summary>
    protected virtual void Validate(SubscriptionPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Name))
        {
            throw new SubscriptionOperationException("Enter a plan name.");
        }

        if (string.IsNullOrWhiteSpace(plan.StoreId))
        {
            throw new SubscriptionOperationException("Choose a store.");
        }

        if (plan.Intervals == null || plan.Intervals.Count == 0)
        {
            throw new SubscriptionOperationException("Add at least one delivery frequency.");
        }

        if (plan.Intervals.Any(x => x.Count < 1 || x.Count > 365))
        {
            throw new SubscriptionOperationException("Each frequency must be between 1 and 365.");
        }

        if (plan.Intervals.GroupBy(x => (x.Unit, x.Count)).Any(x => x.Count() > 1))
        {
            throw new SubscriptionOperationException("Each frequency can be listed only once.");
        }

        if (plan.DiscountType == SubscriptionDiscountType.Percent && (plan.DiscountValue < 0 || plan.DiscountValue > 100))
        {
            throw new SubscriptionOperationException("A percent discount must be between 0 and 100.");
        }

        if (plan.DiscountValue < 0)
        {
            throw new SubscriptionOperationException("The discount can't be negative.");
        }

        if (plan.Intervals.Count(x => x.IsDefault) != 1)
        {
            foreach (var interval in plan.Intervals)
            {
                interval.IsDefault = false;
            }

            plan.Intervals[0].IsDefault = true;
        }
    }
}
