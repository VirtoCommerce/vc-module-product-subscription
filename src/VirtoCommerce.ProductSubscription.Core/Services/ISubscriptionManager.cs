using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
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

    /// <summary>
    /// Makes a card saved with a checkout order pay that order's subscriptions; the card can be saved after the
    /// subscriptions were activated with another card of the customer.
    /// </summary>
    Task<IList<Subscription>> LinkStoredPaymentMethodAsync(StoredPaymentMethod storedPaymentMethod);

    /// <summary>
    /// Skips the next delivery. <paramref name="expectedNextOrderDate"/> is the delivery the customer saw: when given and
    /// the subscription has moved on since (another skip, a change), the skip is refused instead of skipping a later one.
    /// </summary>
    Task<Subscription> SkipNextAsync(string subscriptionId, System.DateTime? expectedNextOrderDate = null);
    Task<Subscription> PauseAsync(string subscriptionId, System.DateTime? resumeDate);
    Task<Subscription> ResumeAsync(string subscriptionId);
    Task<Subscription> CancelAsync(string subscriptionId, string reason);
    Task<Subscription> ChangeAsync(string subscriptionId, SubscriptionChange change);
}
