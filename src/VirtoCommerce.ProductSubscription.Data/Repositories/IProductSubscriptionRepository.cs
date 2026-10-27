using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
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
