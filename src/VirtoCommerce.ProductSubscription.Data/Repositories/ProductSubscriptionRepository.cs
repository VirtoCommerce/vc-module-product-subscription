using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.ProductSubscription.Data.Models;

namespace VirtoCommerce.ProductSubscription.Data.Repositories;

public interface IProductSubscriptionRepository : IRepository
{
    IQueryable<SubscriptionPlanEntity> Plans { get; }
    IQueryable<SubscriptionPlanAssignmentEntity> PlanAssignments { get; }
    IQueryable<SubscriptionEntity> Subscriptions { get; }
    IQueryable<RenewalEntity> Renewals { get; }
    IQueryable<RenewalItemEntity> RenewalItems { get; }
    IQueryable<SubscriptionCartItemEntity> CartItems { get; }

    Task<IList<SubscriptionPlanEntity>> GetPlansByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<SubscriptionEntity>> GetSubscriptionsByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<RenewalEntity>> GetRenewalsByIdsAsync(IList<string> ids, string responseGroup);
    Task<IList<SubscriptionCartItemEntity>> GetCartItemsByIdsAsync(IList<string> ids, string responseGroup);
}

public class ProductSubscriptionRepository(ProductSubscriptionDbContext dbContext, IUnitOfWork unitOfWork = null)
    : DbContextRepositoryBase<ProductSubscriptionDbContext>(dbContext, unitOfWork), IProductSubscriptionRepository
{
    public IQueryable<SubscriptionPlanEntity> Plans => DbContext.Set<SubscriptionPlanEntity>();
    public IQueryable<SubscriptionPlanAssignmentEntity> PlanAssignments => DbContext.Set<SubscriptionPlanAssignmentEntity>();
    public IQueryable<SubscriptionEntity> Subscriptions => DbContext.Set<SubscriptionEntity>();
    public IQueryable<RenewalEntity> Renewals => DbContext.Set<RenewalEntity>();
    public IQueryable<RenewalItemEntity> RenewalItems => DbContext.Set<RenewalItemEntity>();
    public IQueryable<SubscriptionCartItemEntity> CartItems => DbContext.Set<SubscriptionCartItemEntity>();

    public virtual async Task<IList<SubscriptionPlanEntity>> GetPlansByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return await Plans
            .Include(x => x.Intervals)
            .Include(x => x.Assignments)
            .Where(x => ids.Contains(x.Id))
            .AsSplitQuery()
            .ToListAsync();
    }

    public virtual async Task<IList<SubscriptionEntity>> GetSubscriptionsByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return await Subscriptions.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual async Task<IList<RenewalEntity>> GetRenewalsByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return await Renewals
            .Include(x => x.Items)
            .Include(x => x.Events)
            .Where(x => ids.Contains(x.Id))
            .AsSplitQuery()
            .ToListAsync();
    }

    public virtual async Task<IList<SubscriptionCartItemEntity>> GetCartItemsByIdsAsync(IList<string> ids, string responseGroup)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return await CartItems.Where(x => ids.Contains(x.Id)).ToListAsync();
    }
}
