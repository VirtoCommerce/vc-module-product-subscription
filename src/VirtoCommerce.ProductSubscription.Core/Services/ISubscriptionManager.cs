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

    /// <summary>
    /// Creates the subscriptions of a new order from the given cart choices, e.g. a copy taken before the cart change
    /// that follows checkout deleted them. Choices of another cart are ignored.
    /// </summary>
    Task<IList<Subscription>> CreateFromOrderAsync(CustomerOrder order, IList<SubscriptionCartItem> choices);

    /// <summary>
    /// Creates the subscriptions from a copy of the cart's choices, with the consent given for the order's checkout (taken
    /// before xOrder replaced the cart's CheckoutId).
    /// </summary>
    Task<IList<Subscription>> CreateFromOrderAsync(CustomerOrder order, IList<SubscriptionCartItem> choices, string consentId);

    /// <summary>The customer's consent to the subscription terms for the cart's current checkout, or null.</summary>
    Task<string> GetCheckoutConsentIdAsync(string cartId);

    /// <summary>The subscription choices saved for a cart's lines.</summary>
    Task<IList<SubscriptionCartItem>> GetCartChoicesAsync(string cartId);
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
