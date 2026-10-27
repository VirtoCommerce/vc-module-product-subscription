using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using Address = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.ProductSubscription.Data.Import;

/// <summary>
/// Reads subscriptions in the JSON the old Virto subscription module returns from <c>POST api/subscriptions/search</c>
/// (or <c>GET api/subscriptions/{id}</c>), and turns them into item-level <see cref="LegacySubscriptionImportRecord"/>s.
/// Works on raw JSON, so this module never references the old module's assemblies.
/// </summary>
/// <remarks>
/// Accepted shapes: a search result (<c>{ "totalCount": n, "results": [...] }</c>), an array of subscriptions, a single
/// subscription, or a platform export (<c>{ "Subscriptions": [...] }</c>). Property names are matched case-insensitively.
/// The old module only knows the products through the order prototype, so the subscriptions must be read with the
/// <c>WithOrderPrototype</c> response group.
/// </remarks>
public class LegacySubscriptionJsonMapper
{
    public const string OuterIdPrefix = "legacy:";

    private static readonly string[] _legacyMarkers = ["subscriptionStatus", "customerOrderPrototype", "customerOrderPrototypeId", "currentPeriodEnd", "interval"];
    private static readonly string[] _recordMarkers = ["legacySubscriptionId", "intervalUnit"];

    protected virtual DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Reads the import request body: either import records or the old module's JSON, detected by shape.
    /// </summary>
    public virtual IList<LegacySubscriptionImportRecord> ReadRecords(JToken json)
    {
        var items = GetItems(json);

        return IsLegacyModuleJson(json)
            ? items.SelectMany(MapSubscription).ToList()
            : items.Select(ToRecord).ToList();
    }

    /// <summary>
    /// True when the JSON comes from the old module (a search result, an export or old subscriptions), false when it is
    /// a list of <see cref="LegacySubscriptionImportRecord"/>s.
    /// </summary>
    public virtual bool IsLegacyModuleJson(JToken json)
    {
        if (json is JObject obj && (GetValue(obj, "results") is JArray || GetValue(obj, "subscriptions") is JArray))
        {
            return true;
        }

        var items = GetItems(json);
        var first = items.FirstOrDefault();
        if (first == null)
        {
            return false;
        }

        // An import record has "intervalUnit"/"legacySubscriptionId"; an old subscription has "interval", "subscriptionStatus"...
        if (_recordMarkers.Any(x => GetValue(first, x) != null))
        {
            return false;
        }

        return _legacyMarkers.Any(x => GetValue(first, x) != null);
    }

    /// <summary>Maps old-module subscriptions (any accepted shape) to import records.</summary>
    public virtual IList<LegacySubscriptionImportRecord> Map(JToken json)
    {
        return GetItems(json).SelectMany(MapSubscription).ToList();
    }

    /// <summary>
    /// Maps one old subscription: one record per product in its order prototype. Gift and cancelled lines are left out,
    /// and lines of the same product are merged. A subscription without products gives one record without a product,
    /// which the importer reports as failed.
    /// </summary>
    public virtual IList<LegacySubscriptionImportRecord> MapSubscription(JObject subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var prototype = GetValue(subscription, "customerOrderPrototype") as JObject;
        var template = CreateRecord(subscription, prototype);

        var lines = (GetValue(prototype, "items") as JArray ?? [])
            .OfType<JObject>()
            .Where(x => !string.IsNullOrEmpty(GetString(x, "productId")) && GetBool(x, "isGift") != true && GetBool(x, "isCancelled") != true)
            .GroupBy(x => GetString(x, "productId"))
            .ToList();

        if (lines.Count == 0)
        {
            template.OuterId = BuildOuterId(template.LegacySubscriptionId, productId: null);
            return [template];
        }

        var result = new List<LegacySubscriptionImportRecord>();
        foreach (var line in lines)
        {
            var first = line.First();
            var record = (LegacySubscriptionImportRecord)template.Clone();
            record.ProductId = line.Key;
            record.Sku = GetString(first, "sku");
            record.ProductName = GetString(first, "name");
            record.ImageUrl = GetString(first, "imageUrl");
            record.Quantity = Math.Max(1, line.Sum(x => GetInt(x, "quantity") ?? 1));
            record.ReferencePrice = GetDecimal(first, "placedPrice") ?? GetDecimal(first, "price");
            record.OuterId = BuildOuterId(template.LegacySubscriptionId, lines.Count > 1 ? line.Key : null);
            result.Add(record);
        }

        return result;
    }

    public static string BuildOuterId(string legacySubscriptionId, string productId)
    {
        if (string.IsNullOrEmpty(legacySubscriptionId))
        {
            return null;
        }

        return string.IsNullOrEmpty(productId)
            ? $"{OuterIdPrefix}{legacySubscriptionId}"
            : $"{OuterIdPrefix}{legacySubscriptionId}:{productId}";
    }

    /// <summary>Old <c>PaymentInterval</c> (Days, Weeks, Months, Years; by name or number) to this module's unit.</summary>
    public static SubscriptionIntervalUnit? ParseInterval(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type == JTokenType.Integer)
        {
            return (LegacyPaymentInterval)token.Value<int>() switch
            {
                LegacyPaymentInterval.Days => SubscriptionIntervalUnit.Day,
                LegacyPaymentInterval.Weeks => SubscriptionIntervalUnit.Week,
                LegacyPaymentInterval.Months => SubscriptionIntervalUnit.Month,
                LegacyPaymentInterval.Years => SubscriptionIntervalUnit.Year,
                _ => null,
            };
        }

        return token.ToString().Trim().ToLowerInvariant() switch
        {
            "days" or "day" => SubscriptionIntervalUnit.Day,
            "weeks" or "week" => SubscriptionIntervalUnit.Week,
            "months" or "month" => SubscriptionIntervalUnit.Month,
            "years" or "year" => SubscriptionIntervalUnit.Year,
            _ => null,
        };
    }

    /// <summary>Old <c>SubscriptionStatus</c> (by name or number: Active=0, Trialing=1, PastDue=2, Cancelled=3, Unpaid=4).</summary>
    public static LegacySubscriptionStatus? ParseStatus(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type == JTokenType.Integer)
        {
            var value = token.Value<int>();
            return Enum.IsDefined(typeof(LegacySubscriptionStatus), value) ? (LegacySubscriptionStatus)value : null;
        }

        return Enum.TryParse<LegacySubscriptionStatus>(token.ToString().Trim(), ignoreCase: true, out var status) ? status : null;
    }

    protected virtual LegacySubscriptionImportRecord CreateRecord(JObject subscription, JObject prototype)
    {
        var now = UtcNow;
        var record = AbstractTypeFactory<LegacySubscriptionImportRecord>.TryCreateInstance();

        record.LegacySubscriptionId = GetString(subscription, "id");
        record.LegacySubscriptionNumber = GetString(subscription, "number");
        record.StoreId = GetString(subscription, "storeId") ?? GetString(prototype, "storeId");
        record.CustomerId = GetString(subscription, "customerId") ?? GetString(prototype, "customerId");
        record.CustomerName = GetString(subscription, "customerName") ?? GetString(prototype, "customerName");
        record.OrganizationId = GetString(prototype, "organizationId");
        record.Currency = GetString(prototype, "currency");
        record.LanguageCode = GetString(prototype, "languageCode");

        record.IntervalUnit = ParseInterval(GetValue(subscription, "interval")) ?? SubscriptionIntervalUnit.Month;
        record.IntervalCount = Math.Max(1, GetInt(subscription, "intervalCount") ?? 1);

        record.StartDate = GetDate(subscription, "startDate") ?? GetDate(subscription, "createdDate");
        record.LastOrderDate = GetDate(subscription, "currentPeriodStart");
        record.NextOrderDate = GetDate(subscription, "currentPeriodEnd")
            ?? AddInterval(record.LastOrderDate, record.IntervalUnit, record.IntervalCount)
            ?? AddInterval(record.StartDate, record.IntervalUnit, record.IntervalCount);

        record.Status = GetStatus(subscription, now, out var cancelledDate, out var cancelReason);
        record.CancelledDate = cancelledDate;
        record.CancelReason = cancelReason;

        var shipment = (GetValue(prototype, "shipments") as JArray)?.OfType<JObject>().FirstOrDefault();
        var payment = (GetValue(prototype, "inPayments") as JArray)?.OfType<JObject>().FirstOrDefault();
        var addresses = (GetValue(prototype, "addresses") as JArray)?.OfType<JObject>().ToList() ?? [];

        record.ShippingAddress = ToAddress(GetValue(shipment, "deliveryAddress") as JObject)
            ?? ToAddress(addresses.FirstOrDefault(x => IsShippingAddress(GetValue(x, "addressType"))))
            ?? ToAddress(addresses.FirstOrDefault());
        record.ShippingMethodCode = GetString(shipment, "shipmentMethodCode");
        record.ShippingMethodOption = GetString(shipment, "shipmentMethodOption");
        record.PaymentMethodCode = GetString(payment, "gatewayCode");

        record.CustomerEmail = record.ShippingAddress?.Email
            ?? addresses.Select(x => GetString(x, "email")).FirstOrDefault(x => !string.IsNullOrEmpty(x))
            ?? GetString(GetValue(payment, "billingAddress") as JObject, "email");

        return record;
    }

    /// <summary>
    /// The old module's own rules (SubscriptionBuilder.ActualizeAsync): a cancelled flag or an end date that has passed
    /// means cancelled; an outstanding balance on an active subscription means unpaid.
    /// </summary>
    protected virtual LegacySubscriptionStatus GetStatus(JObject subscription, DateTime now, out DateTime? cancelledDate, out string cancelReason)
    {
        cancelledDate = GetDate(subscription, "cancelledDate");
        cancelReason = GetString(subscription, "cancelReason");

        var status = ParseStatus(GetValue(subscription, "subscriptionStatus")) ?? LegacySubscriptionStatus.Active;
        if (GetBool(subscription, "isCancelled") == true || status == LegacySubscriptionStatus.Cancelled)
        {
            return LegacySubscriptionStatus.Cancelled;
        }

        var endDate = GetDate(subscription, "endDate");
        if (endDate != null && endDate <= now)
        {
            cancelledDate ??= endDate;
            cancelReason ??= "Completed with time expiration";
            return LegacySubscriptionStatus.Cancelled;
        }

        if (status == LegacySubscriptionStatus.Active && GetDecimal(subscription, "balance") > 0)
        {
            return LegacySubscriptionStatus.Unpaid;
        }

        return status;
    }

    protected virtual LegacySubscriptionImportRecord ToRecord(JObject item)
    {
        var record = AbstractTypeFactory<LegacySubscriptionImportRecord>.TryCreateInstance();
        using var reader = item.CreateReader();
        JsonSerializer.CreateDefault().Populate(reader, record);
        return record;
    }

    protected virtual Address ToAddress(JObject source)
    {
        if (source == null || !source.HasValues)
        {
            return null;
        }

        var address = AbstractTypeFactory<Address>.TryCreateInstance();
        address.AddressType = ParseAddressType(GetValue(source, "addressType")) ?? AddressType.Shipping;
        address.Name = GetString(source, "name");
        address.FirstName = GetString(source, "firstName");
        address.MiddleName = GetString(source, "middleName");
        address.LastName = GetString(source, "lastName");
        address.Organization = GetString(source, "organization");
        address.Line1 = GetString(source, "line1");
        address.Line2 = GetString(source, "line2");
        address.City = GetString(source, "city");
        address.PostalCode = GetString(source, "postalCode") ?? GetString(source, "zip");
        address.RegionId = GetString(source, "regionId");
        address.RegionName = GetString(source, "regionName");
        address.CountryCode = GetString(source, "countryCode");
        address.CountryName = GetString(source, "countryName");
        address.Phone = GetString(source, "phone");
        address.Email = GetString(source, "email");
        address.OuterId = GetString(source, "outerId");
        return address;
    }

    protected static IList<JObject> GetItems(JToken json)
    {
        return json switch
        {
            JArray array => array.OfType<JObject>().ToList(),
            JObject obj when GetValue(obj, "results") is JArray results => results.OfType<JObject>().ToList(),
            JObject obj when GetValue(obj, "subscriptions") is JArray subscriptions => subscriptions.OfType<JObject>().ToList(),
            JObject obj => [obj],
            _ => [],
        };
    }

    [SuppressMessage("Major Code Smell", "S1168:Empty arrays and collections should be returned instead of null",
        Justification = "A JToken is a value, not a collection: null means the property is missing or null.")]
    protected static JToken GetValue(JObject source, string name)
    {
        var value = source?.GetValue(name, StringComparison.OrdinalIgnoreCase);
        return value == null || value.Type == JTokenType.Null ? null : value;
    }

    protected static string GetString(JObject source, string name)
    {
        var value = GetValue(source, name);
        if (value == null)
        {
            return null;
        }

        var text = value.Type == JTokenType.Date
            ? value.Value<DateTime>().ToString("O", CultureInfo.InvariantCulture)
            : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    protected static int? GetInt(JObject source, string name)
    {
        var value = GetValue(source, name);
        return value?.Type switch
        {
            JTokenType.Integer => value.Value<int>(),
            JTokenType.Float => (int)Math.Round(value.Value<double>()),
            JTokenType.String => int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null,
            _ => null,
        };
    }

    protected static decimal? GetDecimal(JObject source, string name)
    {
        var value = GetValue(source, name);
        return value?.Type switch
        {
            JTokenType.Integer or JTokenType.Float => value.Value<decimal>(),
            JTokenType.String => decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null,
            _ => null,
        };
    }

    protected static bool? GetBool(JObject source, string name)
    {
        var value = GetValue(source, name);
        return value?.Type switch
        {
            JTokenType.Boolean => value.Value<bool>(),
            JTokenType.String => bool.TryParse(value.ToString(), out var flag) ? flag : null,
            _ => null,
        };
    }

    /// <summary>Reads a date as UTC; a date without a time zone is taken as UTC, as the platform stores dates.</summary>
    protected static DateTime? GetDate(JObject source, string name)
    {
        var value = GetValue(source, name);
        DateTime date;

        switch (value?.Type)
        {
            case JTokenType.Date:
                date = value.Value<DateTime>();
                break;
            case JTokenType.String when DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed):
                date = parsed;
                break;
            default:
                return null;
        }

        return date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc),
        };
    }

    private static DateTime? AddInterval(DateTime? date, SubscriptionIntervalUnit unit, int count)
    {
        return date == null ? null : SubscriptionSchedule.AddInterval(date.Value, unit, count);
    }

    private static AddressType? ParseAddressType(JToken token)
    {
        if (token == null)
        {
            return null;
        }

        if (token.Type == JTokenType.Integer)
        {
            var value = token.Value<int>();
            return Enum.IsDefined(typeof(AddressType), value) ? (AddressType)value : null;
        }

        return Enum.TryParse<AddressType>(token.ToString(), ignoreCase: true, out var type) ? type : null;
    }

    private static bool IsShippingAddress(JToken addressType)
    {
        var type = ParseAddressType(addressType);
        return type is AddressType.Shipping or AddressType.BillingAndShipping;
    }
}
