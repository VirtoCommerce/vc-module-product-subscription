using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Address = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.ProductSubscription.Data.Import;

/// <summary>
/// Creates item-level subscriptions from records exported from the old subscription module.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="bullet">
/// <item>Idempotent: a record whose OuterId (<c>legacy:{oldId}</c> by default) already exists is skipped.</item>
/// <item>Statuses: Active and Trialing become Active, PastDue and Unpaid become PastDue, Cancelled stays Cancelled.</item>
/// <item>Legacy subscriptions have no saved card: StoredPaymentMethodId stays empty and the old payment method code is kept.</item>
/// <item>The plan is the active plan the store offers for the product, when it offers the legacy frequency;
/// otherwise the subscription has no plan and keeps the legacy frequency.</item>
/// <item>The next order date is never in the past: it is moved to tomorrow.</item>
/// <item>No emails are sent; a dry run validates and reports without saving.</item>
/// </list>
/// </remarks>
public class LegacySubscriptionImporter(
    ISubscriptionService subscriptionService,
    ISubscriptionSearchService subscriptionSearchService,
    ISubscriptionOfferService offerService,
    IStoreService storeService,
    IItemService itemService,
    ILogger<LegacySubscriptionImporter> logger)
    : ILegacySubscriptionImporter
{
    public const int BatchSize = 50;
    public const int MaxOuterIdLength = 128;
    public const int MaxIntervalCount = 365;
    public const int MaxQuantity = 9999;

    protected virtual DateTime UtcNow => DateTime.UtcNow;

    public virtual async Task<SubscriptionImportResult> ImportAsync(IList<LegacySubscriptionImportRecord> records, bool dryRun, bool applyPlanDiscount = false)
    {
        var result = AbstractTypeFactory<SubscriptionImportResult>.TryCreateInstance();
        result.DryRun = dryRun;

        if (records.IsNullOrEmpty())
        {
            return result;
        }

        var now = UtcNow;
        var entries = records.Select(x => new ImportEntry(x, CreateRecordResult(x)) { ApplyPlanDiscount = applyPlanDiscount }).ToList();
        result.TotalCount = entries.Count;
        result.Records = entries.Select(x => x.Result).ToList();

        foreach (var entry in entries)
        {
            Validate(entry);
        }

        FailDuplicatesInBatch(entries);
        await SkipExistingAsync(entries);

        var pending = entries.Where(x => x.IsPending).ToList();
        var stores = await LoadStoresAsync(pending);
        var products = await LoadProductsAsync(pending);
        var offers = await LoadOffersAsync(pending, stores);

        foreach (var entry in pending.Where(x => x.IsPending))
        {
            stores.TryGetValue(entry.Record.StoreId, out var store);
            products.TryGetValue(entry.Record.ProductId, out var product);
            offers.TryGetValue((entry.Record.StoreId, entry.Record.ProductId), out var offer);

            try
            {
                entry.Subscription = BuildSubscription(entry, store, product, offer, now);
            }
            catch (Exception ex)
            {
                Fail(entry, ex.Message);
            }
        }

        var toSave = entries.Where(x => x.IsPending && x.Subscription != null).ToList();
        if (!dryRun)
        {
            await SaveAsync(toSave);
        }

        foreach (var entry in entries.Where(x => x.IsPending))
        {
            entry.Result.Outcome = SubscriptionImportOutcome.Created;
            entry.Result.SubscriptionId = entry.Subscription?.Id;
            entry.Result.SubscriptionNumber = entry.Subscription?.Number;
        }

        result.CreatedCount = entries.Count(x => x.Result.Outcome == SubscriptionImportOutcome.Created);
        result.SkippedCount = entries.Count(x => x.Result.Outcome == SubscriptionImportOutcome.SkippedDuplicate);
        result.FailedCount = entries.Count(x => x.Result.Outcome == SubscriptionImportOutcome.Failed);

        logger.LogInformation("Legacy subscription import{DryRun}: {Created} created, {Skipped} skipped as duplicates, {Failed} failed of {Total}",
            dryRun ? " (dry run)" : string.Empty, result.CreatedCount, result.SkippedCount, result.FailedCount, result.TotalCount);

        return result;
    }

    public static string MapStatus(LegacySubscriptionStatus status)
    {
        return status switch
        {
            LegacySubscriptionStatus.Active or LegacySubscriptionStatus.Trialing => SubscriptionStatus.Active,
            LegacySubscriptionStatus.PastDue or LegacySubscriptionStatus.Unpaid => SubscriptionStatus.PastDue,
            LegacySubscriptionStatus.Cancelled => SubscriptionStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    public static string GetOuterId(LegacySubscriptionImportRecord record)
    {
        return !string.IsNullOrWhiteSpace(record.OuterId)
            ? record.OuterId.Trim()
            : LegacySubscriptionJsonMapper.BuildOuterId(record.LegacySubscriptionId, productId: null);
    }

    protected virtual SubscriptionImportRecordResult CreateRecordResult(LegacySubscriptionImportRecord record)
    {
        var recordResult = AbstractTypeFactory<SubscriptionImportRecordResult>.TryCreateInstance();
        recordResult.LegacySubscriptionId = record?.LegacySubscriptionId;
        recordResult.LegacySubscriptionNumber = record?.LegacySubscriptionNumber;
        recordResult.ProductId = record?.ProductId;
        recordResult.OuterId = record == null ? null : GetOuterId(record);
        return recordResult;
    }

    protected virtual void Validate(ImportEntry entry)
    {
        var record = entry.Record;
        if (record == null)
        {
            Fail(entry, "The record is empty.");
            return;
        }

        var errors = new List<string>();

        if (string.IsNullOrEmpty(entry.Result.OuterId))
        {
            errors.Add("The legacy subscription id (or OuterId) is required.");
        }
        else if (entry.Result.OuterId.Length > MaxOuterIdLength)
        {
            errors.Add($"The OuterId is longer than {MaxOuterIdLength} characters.");
        }

        if (string.IsNullOrEmpty(record.StoreId))
        {
            errors.Add("The store is required.");
        }

        if (string.IsNullOrEmpty(record.CustomerId))
        {
            errors.Add("The customer is required.");
        }

        if (string.IsNullOrEmpty(record.ProductId))
        {
            errors.Add("The product is required: the legacy subscription has no products in its order prototype.");
        }

        if (record.Quantity < 1 || record.Quantity > MaxQuantity)
        {
            errors.Add($"The quantity must be between 1 and {MaxQuantity}.");
        }

        if (!Enum.IsDefined(record.IntervalUnit))
        {
            errors.Add("The frequency unit is not valid.");
        }

        if (record.IntervalCount < 1 || record.IntervalCount > MaxIntervalCount)
        {
            errors.Add($"The frequency must be between 1 and {MaxIntervalCount}.");
        }

        if (!Enum.IsDefined(record.Status))
        {
            errors.Add("The status is not valid.");
        }

        if (record.DiscountType == SubscriptionDiscountType.Percent && record.DiscountValue is < 0 or > 100)
        {
            errors.Add("A percent discount must be between 0 and 100.");
        }
        else if (record.DiscountValue < 0)
        {
            errors.Add("The discount can't be negative.");
        }

        if (errors.Count > 0)
        {
            Fail(entry, errors.ToArray());
        }
    }

    protected virtual void FailDuplicatesInBatch(IList<ImportEntry> entries)
    {
        var duplicates = entries
            .Where(x => x.IsPending)
            .GroupBy(x => x.Result.OuterId, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1);

        foreach (var group in duplicates)
        {
            foreach (var entry in group.Skip(1))
            {
                Fail(entry, $"Another record in this import has the same OuterId {entry.Result.OuterId}.");
            }
        }
    }

    protected virtual async Task SkipExistingAsync(IList<ImportEntry> entries)
    {
        var pending = entries.Where(x => x.IsPending).ToList();

        foreach (var batch in pending.Chunk(BatchSize))
        {
            var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
            criteria.OuterIds = batch.Select(x => x.Result.OuterId).ToList();
            criteria.Take = batch.Length * 2;

            var existing = (await subscriptionSearchService.SearchAsync(criteria, clone: false)).Results
                .Where(x => !string.IsNullOrEmpty(x.OuterId))
                .GroupBy(x => x.OuterId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var entry in batch)
            {
                if (existing.TryGetValue(entry.Result.OuterId, out var subscription))
                {
                    entry.Result.Outcome = SubscriptionImportOutcome.SkippedDuplicate;
                    entry.Result.SubscriptionId = subscription.Id;
                    entry.Result.SubscriptionNumber = subscription.Number;
                    entry.Result.Status = subscription.Status;
                    entry.Result.PlanId = subscription.PlanId;
                    entry.Result.PlanName = subscription.PlanName;
                    entry.Result.NextOrderDate = subscription.NextOrderDate;
                    entry.Result.Messages.Add($"Already imported as subscription {subscription.Number}.");
                }
            }
        }
    }

    protected virtual async Task<IDictionary<string, Store>> LoadStoresAsync(IList<ImportEntry> entries)
    {
        var storeIds = entries.Select(x => x.Record.StoreId).Distinct().ToList();
        var stores = storeIds.Count == 0
            ? new Dictionary<string, Store>()
            : (await storeService.GetNoCloneAsync(storeIds)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.Where(x => !stores.ContainsKey(x.Record.StoreId)))
        {
            Fail(entry, $"Store {entry.Record.StoreId} was not found.");
        }

        return stores;
    }

    protected virtual async Task<IDictionary<string, CatalogProduct>> LoadProductsAsync(IList<ImportEntry> entries)
    {
        var productIds = entries.Where(x => x.IsPending).Select(x => x.Record.ProductId).Distinct().ToList();
        var products = new Dictionary<string, CatalogProduct>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in productIds.Chunk(BatchSize))
        {
            foreach (var product in await itemService.GetNoCloneAsync(batch, ItemResponseGroup.ItemInfo.ToString()))
            {
                products[product.Id] = product;
            }
        }

        // A cancelled subscription is history: keep it even when its product has been deleted.
        foreach (var entry in entries.Where(x => x.IsPending && !products.ContainsKey(x.Record.ProductId)))
        {
            if (entry.Record.Status == LegacySubscriptionStatus.Cancelled)
            {
                entry.Result.Messages.Add($"Product {entry.Record.ProductId} was not found; the cancelled subscription is imported anyway.");
            }
            else
            {
                Fail(entry, $"Product {entry.Record.ProductId} was not found.");
            }
        }

        return products;
    }

    protected virtual async Task<IDictionary<(string StoreId, string ProductId), SubscriptionOffer>> LoadOffersAsync(IList<ImportEntry> entries, IDictionary<string, Store> stores)
    {
        var result = new Dictionary<(string, string), SubscriptionOffer>();

        foreach (var storeGroup in entries.Where(x => x.IsPending).GroupBy(x => x.Record.StoreId))
        {
            var productIds = storeGroup.Select(x => x.Record.ProductId).Distinct().ToList();
            var offers = await offerService.GetOffersAsync(stores[storeGroup.Key].Id, productIds);
            foreach (var (productId, offer) in offers)
            {
                result[(storeGroup.Key, productId)] = offer;
            }
        }

        return result;
    }

    protected virtual Subscription BuildSubscription(ImportEntry entry, Store store, CatalogProduct product, SubscriptionOffer offer, DateTime now)
    {
        var record = entry.Record;
        var messages = entry.Result.Messages;

        var subscription = AbstractTypeFactory<Subscription>.TryCreateInstance();
        subscription.OuterId = entry.Result.OuterId;
        subscription.StoreId = store.Id;
        subscription.CustomerId = record.CustomerId;
        subscription.CustomerName = record.CustomerName;
        subscription.CustomerEmail = record.CustomerEmail;
        subscription.OrganizationId = record.OrganizationId;

        subscription.ProductId = record.ProductId;
        subscription.Sku = record.Sku ?? product?.Code;
        subscription.ProductName = record.ProductName ?? product?.Name;
        subscription.ImageUrl = record.ImageUrl ?? product?.ImgSrc;
        subscription.Quantity = record.Quantity;

        subscription.IntervalUnit = record.IntervalUnit;
        subscription.IntervalCount = record.IntervalCount;
        ApplyPlan(subscription, record, offer, messages, entry.ApplyPlanDiscount);

        subscription.Status = MapStatus(record.Status);
        subscription.StartDate = ToUtc(record.StartDate) ?? now;
        subscription.LastOrderDate = ToUtc(record.LastOrderDate);
        // Keep the customer's legacy billing day for later monthly deliveries.
        subscription.AnchorDay = (ToUtc(record.NextOrderDate) ?? subscription.StartDate).Day;
        subscription.NextOrderDate = GetNextOrderDate(subscription, record, now, messages, GetReminderLeadDays(store));

        if (subscription.Status == SubscriptionStatus.Cancelled)
        {
            subscription.CancelledDate = ToUtc(record.CancelledDate) ?? now;
            subscription.CancelReason = string.IsNullOrWhiteSpace(record.CancelReason) ? null : record.CancelReason.Trim();
        }

        subscription.Currency = record.Currency ?? store.DefaultCurrency;
        subscription.LanguageCode = record.LanguageCode ?? store.DefaultLanguage;
        subscription.ShippingAddress = (Address)record.ShippingAddress?.Clone();
        if (subscription.ShippingAddress != null)
        {
            subscription.ShippingAddress.Key = null;
        }

        subscription.ShippingMethodCode = record.ShippingMethodCode;
        subscription.ShippingMethodOption = record.ShippingMethodOption;

        // The old module never saved cards. Without one, RenewalProcessor.ChargeAsync places the order but doesn't charge
        // it: the renewal completes with "payment is collected separately" until a saved card is set (docs/legacy-import.md).
        subscription.PaymentMethodCode = record.PaymentMethodCode;
        subscription.StoredPaymentMethodId = null;
        subscription.ConsentId = null;

        subscription.ReferencePrice = record.ReferencePrice;
        // Count the import as the latest annual reminder, so the first scheduler run doesn't email every old subscriber.
        subscription.LastAnnualReminderDate = now;

        if (subscription.Status == SubscriptionStatus.Active && string.IsNullOrEmpty(subscription.PaymentMethodCode))
        {
            messages.Add("No saved card: each renewal asks the customer to pay on the order page until a card is saved.");
        }

        if (subscription.ShippingAddress == null && subscription.Status != SubscriptionStatus.Cancelled)
        {
            messages.Add("No shipping address: renewals will need a review until an address is set.");
        }

        entry.Result.Status = subscription.Status;
        entry.Result.PlanId = subscription.PlanId;
        entry.Result.PlanName = subscription.PlanName;
        entry.Result.NextOrderDate = subscription.NextOrderDate;

        return subscription;
    }

    /// <summary>
    /// Links the store's plan for the product when it offers the legacy frequency, and takes the discount from the record,
    /// or from the plan when the record has none.
    /// </summary>
    protected virtual void ApplyPlan(Subscription subscription, LegacySubscriptionImportRecord record, SubscriptionOffer offer, IList<string> messages, bool applyPlanDiscount)
    {
        var supportsInterval = offer?.Intervals?.Any(x => x.Unit == record.IntervalUnit && x.Count == record.IntervalCount) == true;

        if (offer != null && supportsInterval)
        {
            subscription.PlanId = offer.PlanId;
            subscription.PlanName = offer.PlanName;
        }
        else if (offer != null)
        {
            messages.Add($"Plan {offer.PlanName} doesn't offer {SubscriptionSchedule.Describe(record.IntervalUnit, record.IntervalCount)}; imported without a plan, keeping the legacy frequency.");
        }
        else
        {
            messages.Add("No active plan offers this product in the store; imported without a plan, keeping the legacy frequency.");
        }

        if (record.DiscountType != null)
        {
            subscription.DiscountType = record.DiscountType.Value;
            subscription.DiscountValue = record.DiscountType == SubscriptionDiscountType.None ? 0 : record.DiscountValue ?? 0;
        }
        else if (subscription.PlanId != null && applyPlanDiscount)
        {
            subscription.DiscountType = offer.DiscountType;
            subscription.DiscountValue = offer.DiscountValue;
        }
        else
        {
            subscription.DiscountType = SubscriptionDiscountType.None;
            subscription.DiscountValue = 0;
        }
    }

    protected static int GetReminderLeadDays(Store store)
    {
        var setting = ModuleConstants.Settings.Store.ReminderLeadDays;
        return store.Settings != null ? store.Settings.GetValue<int>(setting) : Convert.ToInt32(setting.DefaultValue);
    }

    protected virtual DateTime GetNextOrderDate(Subscription subscription, LegacySubscriptionImportRecord record, DateTime now, IList<string> messages, int reminderLeadDays)
    {
        // Leave room for the reminder email before the first charge in the new module.
        var earliest = SubscriptionSchedule.EarliestNextOrderDate(now, reminderLeadDays);
        var next = ToUtc(record.NextOrderDate)
                   ?? SubscriptionSchedule.NextAfter(subscription.LastOrderDate ?? subscription.StartDate, subscription.IntervalUnit, subscription.IntervalCount, now, subscription.EffectiveAnchorDay);

        if (next < earliest)
        {
            if (subscription.Status != SubscriptionStatus.Cancelled)
            {
                messages.Add($"The next order date {next:yyyy-MM-dd} is too soon for a reminder; moved to {earliest:yyyy-MM-dd}.");
            }

            next = earliest;
        }

        return next;
    }

    protected virtual async Task SaveAsync(IList<ImportEntry> entries)
    {
        foreach (var batch in entries.Chunk(BatchSize))
        {
            try
            {
                await subscriptionService.SaveChangesAsync(batch.Select(x => x.Subscription).ToList());
            }
            catch (Exception batchException)
            {
                logger.LogWarning(batchException, "Saving a batch of {Count} imported subscriptions failed; saving them one by one", batch.Length);

                // Save one by one so one bad record doesn't fail the whole batch.
                foreach (var entry in batch)
                {
                    try
                    {
                        entry.Subscription.Id = null;
                        await subscriptionService.SaveChangesAsync([entry.Subscription]);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to import legacy subscription {OuterId}", entry.Result.OuterId);
                        Fail(entry, ex.InnerException?.Message ?? ex.Message);
                    }
                }
            }
        }
    }

    protected static void Fail(ImportEntry entry, params string[] messages)
    {
        entry.Result.Outcome = SubscriptionImportOutcome.Failed;
        entry.Subscription = null;
        foreach (var message in messages)
        {
            entry.Result.Messages.Add(message);
        }
    }

    protected static DateTime? ToUtc(DateTime? date)
    {
        return date?.Kind switch
        {
            null => null,
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date.Value, DateTimeKind.Utc),
        };
    }

    protected class ImportEntry(LegacySubscriptionImportRecord record, SubscriptionImportRecordResult result)
    {
        public LegacySubscriptionImportRecord Record { get; } = record;
        public SubscriptionImportRecordResult Result { get; } = result;
        public Subscription Subscription { get; set; }
        public bool ApplyPlanDiscount { get; init; }

        /// <summary>Neither failed nor skipped so far.</summary>
        public bool IsPending => Result.Outcome == null;
    }
}
