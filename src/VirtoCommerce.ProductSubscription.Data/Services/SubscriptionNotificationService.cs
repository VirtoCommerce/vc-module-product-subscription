using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.NotificationsModule.Core.Extensions;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionNotificationService(
    INotificationSearchService notificationSearchService,
    INotificationSender notificationSender,
    IStoreService storeService,
    IMemberService memberService,
    Func<UserManager<ApplicationUser>> userManagerFactory,
    ICurrencyService currencyService,
    IStoredPaymentMethodService storedPaymentMethodService,
    ILogger<SubscriptionNotificationService> logger)
    : ISubscriptionNotificationService
{
    // "Friday, November 20, 2026" in English, the culture's own long date elsewhere.
    private const string DateFormat = "D";

    public virtual async Task SendAsync<T>(IList<Subscription> subscriptions, Renewal renewal = null, Action<T> configure = null)
        where T : SubscriptionEmailNotificationBase
    {
        try
        {
            var first = subscriptions?.FirstOrDefault();
            var storeId = renewal?.StoreId ?? first?.StoreId;
            if (string.IsNullOrEmpty(storeId))
            {
                return;
            }

            var store = await storeService.GetNoCloneAsync(storeId, StoreResponseGroup.Full.ToString());
            if (store == null)
            {
                return;
            }

            var notification = await notificationSearchService.GetNotificationAsync<T>(new TenantIdentity(store.Id, nameof(Store)));
            if (notification == null || !notification.IsActive.GetValueOrDefault(true))
            {
                return;
            }

            var customerId = renewal?.CustomerId ?? first?.CustomerId;
            var contact = await GetContactAsync(customerId);
            var email = renewal?.CustomerEmail ?? first?.CustomerEmail ?? await GetCustomerEmailAsync(customerId);
            if (string.IsNullOrEmpty(email))
            {
                logger.LogInformation("Skipped {Notification}: no email for customer {CustomerId}", typeof(T).Name, customerId);
                return;
            }

            subscriptions ??= new List<Subscription>();
            var storeUrl = GetStoreUrl(store);
            var languageCode = renewal?.LanguageCode ?? first?.LanguageCode ?? store.DefaultLanguage;

            notification.Subscriptions = subscriptions;
            notification.Renewal = renewal;
            notification.CustomerName = renewal?.CustomerName ?? first?.CustomerName;
            notification.FirstName = contact?.FirstName?.Trim() ?? string.Empty;
            notification.StoreName = store.Name;
            notification.StoreUrl = storeUrl;
            notification.LogoUrl = store.Settings.GetValue<string>(ModuleConstants.Settings.Store.EmailLogoUrl) ?? string.Empty;
            notification.AccentColor = GetAccentColor(store);
            notification.SubscriptionsUrl = $"{storeUrl}account/subscriptions";
            notification.ManageUrl = GetManageUrl(storeUrl, renewal?.Items.FirstOrDefault(x => !x.IsExcluded)?.SubscriptionId ?? first?.Id);
            notification.OrderUrl = string.IsNullOrEmpty(renewal?.OrderId) ? string.Empty : $"{storeUrl}account/orders/{renewal.OrderId}";
            notification.From = store.EmailWithName ?? store.Email;
            notification.To = email;
            notification.LanguageCode = GetTemplateLanguage(notification, languageCode);
            notification.TenantIdentity = renewal != null
                ? new TenantIdentity(renewal.Id, nameof(Renewal))
                : new TenantIdentity(first.Id, nameof(Subscription));

            // Before the texts: an event may pass what they are built from (the skipped date).
            configure?.Invoke(notification);

            await FillTextsAsync(notification, store, subscriptions, renewal, storeUrl, languageCode);

            await notificationSender.ScheduleSendNotificationAsync(notification);
        }
        catch (Exception ex)
        {
            // Notifications must never break the subscription flow.
            logger.LogError(ex, "Failed to send {Notification}", typeof(T).Name);
        }
    }

    public virtual async Task<string> GetStoreUrlAsync(string storeId)
    {
        var store = await storeService.GetNoCloneAsync(storeId, StoreResponseGroup.StoreInfo.ToString());
        return store == null ? "/" : GetStoreUrl(store);
    }

    /// <summary>Formats the dates, amounts, frequencies and the card of the email in its language and the store's currency.</summary>
    protected virtual async Task FillTextsAsync(SubscriptionEmailNotificationBase notification, Store store, IList<Subscription> subscriptions,
        Renewal renewal, string storeUrl, string languageCode)
    {
        var culture = GetCulture(languageCode);
        var timeZone = GetTimeZone(store);
        var currencyCode = renewal?.Currency ?? subscriptions.FirstOrDefault()?.Currency ?? store.DefaultCurrency;
        var currency = await GetCurrencyAsync(currencyCode, culture.Name);
        var subscriptionsById = subscriptions.Where(x => !string.IsNullOrEmpty(x.Id)).ToDictionary(x => x.Id);

        notification.Lines = renewal != null
            ? renewal.Items
                .Where(x => !x.IsExcluded)
                .Select(item => CreateLine(subscriptionsById.GetValueOrDefault(item.SubscriptionId), item, currency, culture, timeZone, languageCode, storeUrl))
                .ToList()
            : subscriptions
                .Select(subscription => CreateLine(subscription, item: null, currency, culture, timeZone, languageCode, storeUrl))
                .ToList();

        if (renewal != null)
        {
            notification.DeliveryDateText = FormatDate(renewal.ScheduledDate, culture, timeZone);
            notification.CutoffDateText = FormatDate(renewal.CutoffDate, culture, timeZone);
            notification.TotalText = FormatMoney(renewal.OrderTotal ?? renewal.NotifiedTotal ?? renewal.PreviewTotal, currency, culture);
        }

        var next = subscriptions
            .Where(x => x.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue)
            .Select(x => (DateTime?)x.NextOrderDate)
            .Min();
        notification.NextDeliveryDateText = next == null ? string.Empty : FormatDate(next.Value, culture, timeZone);

        notification.CardText = await GetCardTextAsync(renewal?.StoredPaymentMethodId ?? subscriptions.FirstOrDefault()?.StoredPaymentMethodId);

        switch (notification)
        {
            case DeliverySkippedEmailNotification { SkippedDate: not null } skipped when renewal == null:
                skipped.DeliveryDateText = FormatDate(skipped.SkippedDate.Value, culture, timeZone);
                break;
            case SubscriptionPausedEmailNotification paused:
                var pausedUntil = subscriptions.Select(x => x.PausedUntil).FirstOrDefault(x => x != null);
                paused.PausedUntilText = pausedUntil == null ? string.Empty : FormatDate(pausedUntil.Value, culture, timeZone);
                break;
        }
    }

    protected virtual SubscriptionEmailLine CreateLine(Subscription subscription, RenewalItem item, Currency currency, CultureInfo culture,
        TimeZoneInfo timeZone, string languageCode, string storeUrl)
    {
        var line = AbstractTypeFactory<SubscriptionEmailLine>.TryCreateInstance();
        line.SubscriptionId = subscription?.Id ?? item?.SubscriptionId;
        line.SubscriptionNumber = subscription?.Number ?? string.Empty;
        line.ProductName = item?.ProductName ?? subscription?.ProductName;
        line.Quantity = item?.Quantity ?? subscription?.Quantity ?? 0;
        line.ManageUrl = GetManageUrl(storeUrl, line.SubscriptionId);
        line.TotalText = item == null ? string.Empty : FormatMoney(item.Total, currency, culture);
        line.IsAvailable = item?.IsAvailable ?? true;
        line.Message = item?.Message ?? string.Empty;

        if (subscription != null)
        {
            line.FrequencyText = SubscriptionEmailTexts.Frequency(languageCode, subscription.IntervalUnit, subscription.IntervalCount);
            line.NextDateText = FormatDate(subscription.NextOrderDate, culture, timeZone);
            line.DiscountText = subscription.DiscountValue > 0 && subscription.DiscountType != SubscriptionDiscountType.None
                ? SubscriptionEmailTexts.Discount(languageCode, subscription.DiscountType,
                    subscription.DiscountType == SubscriptionDiscountType.Percent
                        ? subscription.DiscountValue.ToString("0.##", culture)
                        : FormatMoney(subscription.DiscountValue, currency, culture))
                : string.Empty;
        }
        else
        {
            line.FrequencyText = string.Empty;
            line.NextDateText = string.Empty;
            line.DiscountText = string.Empty;
        }

        return line;
    }

    protected virtual string FormatDate(DateTime date, CultureInfo culture, TimeZoneInfo timeZone)
    {
        var utc = date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone).ToString(DateFormat, culture);
    }

    protected virtual string FormatMoney(decimal amount, Currency currency, CultureInfo culture)
    {
        if (currency != null)
        {
            return new Money(amount, currency).FormattedAmount;
        }

        return amount.ToString("N2", culture);
    }

    protected virtual async Task<Currency> GetCurrencyAsync(string currencyCode, string cultureName)
    {
        if (string.IsNullOrEmpty(currencyCode))
        {
            return null;
        }

        var currencies = (await currencyService.GetAllCurrenciesAsync()).ToList();
        if (!currencies.Any(x => x.Code.EqualsIgnoreCase(currencyCode)))
        {
            return null;
        }

        return currencies.GetCurrencyForLanguage(currencyCode, cultureName);
    }

    /// <summary>"Visa •••• 4242", or empty when the card is unknown.</summary>
    protected virtual async Task<string> GetCardTextAsync(string storedPaymentMethodId)
    {
        if (string.IsNullOrEmpty(storedPaymentMethodId))
        {
            return string.Empty;
        }

        var card = await storedPaymentMethodService.GetNoCloneAsync(storedPaymentMethodId);
        if (card == null || string.IsNullOrEmpty(card.Last4))
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(card.Brand) ? $"•••• {card.Last4}" : $"{card.Brand} •••• {card.Last4}";
    }

    /// <summary>
    /// The language to pick the template by. The platform matches it exactly, while the module ships templates per language
    /// ("de") and stores use cultures ("de-DE"): a template for the culture wins, then one for its language.
    /// </summary>
    protected virtual string GetTemplateLanguage(SubscriptionEmailNotificationBase notification, string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode) || notification.Templates == null)
        {
            return languageCode;
        }

        if (notification.Templates.Any(x => x.LanguageCode.EqualsIgnoreCase(languageCode)))
        {
            return languageCode;
        }

        // The module's Norwegian templates are "no"; stores use "nb-NO" (Bokmål) or "no-NO".
        var language = SubscriptionEmailTexts.GetLanguage(languageCode);
        return notification.Templates.Any(x => x.LanguageCode.EqualsIgnoreCase(language)) ? language : languageCode;
    }

    protected virtual string GetAccentColor(Store store)
    {
        var color = store.Settings.GetValue<string>(ModuleConstants.Settings.Store.EmailAccentColor);
        // The value lands in a style attribute: accept only a plain CSS colour, never markup or other declarations.
        return !string.IsNullOrWhiteSpace(color) && color.All(x => char.IsLetterOrDigit(x) || x is '#' or '(' or ')' or ',' or '.' or ' ' or '%')
            ? color.Trim()
            : ModuleConstants.Settings.Store.EmailAccentColor.DefaultValue?.ToString();
    }

    protected virtual string GetManageUrl(string storeUrl, string subscriptionId)
    {
        var url = $"{storeUrl}account/subscriptions";
        return string.IsNullOrEmpty(subscriptionId) ? url : $"{url}?subscription={Uri.EscapeDataString(subscriptionId)}";
    }

    protected virtual CultureInfo GetCulture(string languageCode)
    {
        if (!string.IsNullOrEmpty(languageCode))
        {
            try
            {
                return CultureInfo.GetCultureInfo(languageCode);
            }
            catch (CultureNotFoundException)
            {
                // Fall back to English below.
            }
        }

        return CultureInfo.GetCultureInfo("en-US");
    }

    /// <summary>The store's time zone, so dates fall on the customer's day rather than the server's; UTC when not set.</summary>
    protected virtual TimeZoneInfo GetTimeZone(Store store)
    {
        if (!string.IsNullOrEmpty(store.TimeZone))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(store.TimeZone);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                logger.LogWarning("Unknown time zone {TimeZone} of store {StoreId}; emails use UTC", store.TimeZone, store.Id);
            }
        }

        return TimeZoneInfo.Utc;
    }

    protected virtual string GetStoreUrl(Store store)
    {
        var url = string.IsNullOrEmpty(store.SecureUrl) ? store.Url : store.SecureUrl;
        if (string.IsNullOrEmpty(url))
        {
            return "/";
        }

        return url.EndsWith('/') ? url : url + "/";
    }

    protected virtual async Task<string> GetCustomerEmailAsync(string customerId)
    {
        if (string.IsNullOrEmpty(customerId))
        {
            return null;
        }

        // Orders keep the customer's user account id, not the contact id: same lookup as the Orders module.
        var member = await memberService.GetByIdAsync(customerId);
        if (member != null)
        {
            return member.Emails?.FirstOrDefault();
        }

        using var userManager = userManagerFactory();
        var user = await userManager.FindByIdAsync(customerId);
        if (!string.IsNullOrEmpty(user?.Email))
        {
            return user.Email;
        }

        var contact = string.IsNullOrEmpty(user?.MemberId) ? null : await memberService.GetByIdAsync(user.MemberId);
        return contact?.Emails?.FirstOrDefault();
    }

    /// <summary>
    /// The customer's contact, for the first name. Orders keep the customer's user account id, not the contact id: same lookup as the Orders module.
    /// </summary>
    protected virtual async Task<Contact> GetContactAsync(string customerId)
    {
        if (string.IsNullOrEmpty(customerId))
        {
            return null;
        }

        if (await memberService.GetByIdAsync(customerId) is Contact contact)
        {
            return contact;
        }

        using var userManager = userManagerFactory();
        var user = await userManager.FindByIdAsync(customerId);
        if (string.IsNullOrEmpty(user?.MemberId))
        {
            return null;
        }

        return await memberService.GetByIdAsync(user.MemberId) as Contact;
    }
}
