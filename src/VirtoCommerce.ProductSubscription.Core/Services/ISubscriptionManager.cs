using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

/// <summary>
/// Customer and admin operations on subscriptions. Every change goes through here so the rules
/// (allowed frequencies, state transitions, open renewals) live in one place.
/// </summary>
public interface ISubscriptionManager
{
    Task<IList<Subscription>> CreateFromOrderAsync(CustomerOrder order);
    Task<IList<Subscription>> ActivateForOrderAsync(CustomerOrder order);

    Task<Subscription> SkipNextAsync(string subscriptionId);
    Task<Subscription> PauseAsync(string subscriptionId, System.DateTime? resumeDate);
    Task<Subscription> ResumeAsync(string subscriptionId);
    Task<Subscription> CancelAsync(string subscriptionId, string reason);
    Task<Subscription> ChangeAsync(string subscriptionId, SubscriptionChange change);
}
