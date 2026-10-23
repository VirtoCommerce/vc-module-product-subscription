using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

public interface ISubscriptionPlanService : ICrudService<SubscriptionPlan>;

public interface ISubscriptionPlanSearchService : ISearchService<SubscriptionPlanSearchCriteria, SubscriptionPlanSearchResult, SubscriptionPlan>;

public interface ISubscriptionService : ICrudService<Subscription>;

public interface ISubscriptionSearchService : ISearchService<SubscriptionSearchCriteria, SubscriptionSearchResult, Subscription>;

public interface IRenewalService : ICrudService<Renewal>;

public interface IRenewalSearchService : ISearchService<RenewalSearchCriteria, RenewalSearchResult, Renewal>;

public interface ISubscriptionCartItemService : ICrudService<SubscriptionCartItem>;

public interface ISubscriptionCartItemSearchService : ISearchService<SubscriptionCartItemSearchCriteria, SubscriptionCartItemSearchResult, SubscriptionCartItem>;

/// <summary>
/// Finds the subscription plan that applies to each product (directly or through its category).
/// </summary>
public interface ISubscriptionOfferService
{
    Task<IDictionary<string, SubscriptionOffer>> GetOffersAsync(string storeId, IList<string> productIds);
}

/// <summary>
/// Store-level subscription terms (auto-renewal disclosure) the customer agrees to at checkout.
/// </summary>
public interface ISubscriptionTermsService
{
    Task<SubscriptionTerms> GetTermsAsync(string storeId, string languageCode = null);
}

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

/// <summary>
/// Moves subscriptions from the old Virto subscription module into this module. Idempotent: a record whose OuterId
/// already exists is skipped, so an import can be run again after fixing the failed records.
/// </summary>
public interface ILegacySubscriptionImporter
{
    /// <param name="records">Item-level records; see <see cref="LegacySubscriptionImportRecord"/>.</param>
    /// <param name="dryRun">Validate and report what would be created without saving anything.</param>
    /// <param name="applyPlanDiscount">
    /// Give imported subscriptions the discount of the matched plan. Off by default: legacy customers keep the price they
    /// agreed to unless the merchant decides otherwise (a record's own discount always applies).
    /// </param>
    Task<SubscriptionImportResult> ImportAsync(IList<LegacySubscriptionImportRecord> records, bool dryRun, bool applyPlanDiscount = false);
}

public class SubscriptionChange
{
    public SubscriptionIntervalUnit? IntervalUnit { get; set; }
    public int? IntervalCount { get; set; }
    public int? Quantity { get; set; }
    public System.DateTime? NextOrderDate { get; set; }
    public string StoredPaymentMethodId { get; set; }
}

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
