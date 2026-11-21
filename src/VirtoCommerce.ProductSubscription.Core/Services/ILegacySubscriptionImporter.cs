using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

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
