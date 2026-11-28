using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

/// <summary>
/// Runs renewals through their states: creates them for due subscriptions, sends reminders,
/// recalculates through xCart, places the order, charges the stored payment method and handles failures.
/// </summary>
public interface IRenewalProcessor
{
    /// <summary>Scheduler tick: create renewals for due subscriptions and advance every renewal that is due.</summary>
    Task<int> ProcessDueAsync();

    /// <summary>Advance one renewal as far as it can go right now.</summary>
    Task<Renewal> ProcessAsync(string renewalId, bool ignoreSchedule = false);

    /// <summary>Start a renewal for one subscription immediately ("order now").</summary>
    Task<Renewal> OrderNowAsync(string subscriptionId);

    Task<Renewal> AcceptReviewAsync(string renewalId);
    Task<Renewal> SkipAsync(string renewalId);
    Task<Renewal> RetryPaymentAsync(string renewalId);
    Task<Renewal> CancelAsync(string renewalId, string reason);

    /// <summary>Called when a renewal order changes (payment completed by the customer, shipment shipped).</summary>
    Task HandleOrderChangedAsync(CustomerOrder order);
}
