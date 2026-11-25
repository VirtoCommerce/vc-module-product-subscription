using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ProductSubscription.Core;

public static class ModuleConstants
{
    public const string ModuleId = "VirtoCommerce.ProductSubscription";

    /// <summary>Code of the mock stored-card payment method used to build and test subscriptions without a gateway sandbox.</summary>
    public const string MockPaymentMethodCode = "MockStoredCardPaymentMethod";

    public const string ConsentPurpose = "subscription";

    /// <summary>
    /// Page size of the module's "load them all" searches (SearchAllAsync / SearchAllNoCloneAsync): every record is read,
    /// this many per database round trip.
    /// </summary>
    public const int SearchPageSize = 100;

    private const int MinutesPerDay = 24 * 60;
    // Most card networks keep an authorization for 7 days.
    private const int DefaultAuthorizationLifetimeDays = 7;
    private const int DefaultReminderLeadDays = 3;
    private const int DefaultCutoffHours = 24;
    // Visa allows 15 attempts per 30 days.
    private const int DefaultMaxRetriesPer30Days = 15;
    private const int DefaultPaymentActionWaitDays = 7;
    private const string DefaultEmailAccentColor = "#1f2933";

    /// <summary>The object a subscription consent is recorded for: the checkout cart the customer accepted the terms in.</summary>
    /// <summary>
    /// A consent is given for one checkout: it is recorded against the cart's CheckoutId, which xOrder replaces after each
    /// order. The cart id itself outlives every order of a signed-in customer, so it can't tell one checkout from the next.
    /// </summary>
    public const string ConsentObjectType = "ShoppingCartCheckout";

    public static class Security
    {
        public static class Permissions
        {
            public const string Access = "product-subscription:access";
            public const string Read = "product-subscription:read";
            public const string Update = "product-subscription:update";
            public const string Delete = "product-subscription:delete";
            public const string ManagePlans = "product-subscription:plans:manage";
            public const string ProcessRenewals = "product-subscription:renewals:process";
            public const string Import = "product-subscription:import";

            public static string[] AllPermissions { get; } = [Access, Read, Update, Delete, ManagePlans, ProcessRenewals, Import];
        }
    }

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor SchedulerEnabled { get; } = new()
            {
                Name = "ProductSubscription.Scheduler.Enabled",
                GroupName = "Product Subscription|Scheduler",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
            };

            public static SettingDescriptor SchedulerCronExpression { get; } = new()
            {
                Name = "ProductSubscription.Scheduler.CronExpression",
                GroupName = "Product Subscription|Scheduler",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "*/5 * * * *",
            };

            public static SettingDescriptor SubscriptionNumberTemplate { get; } = new()
            {
                Name = "ProductSubscription.NumberTemplate",
                GroupName = "Product Subscription|General",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "SUB{0:yyMMdd}-{1:D5}",
            };

            public static SettingDescriptor RenewalNumberTemplate { get; } = new()
            {
                Name = "ProductSubscription.Renewal.NumberTemplate",
                GroupName = "Product Subscription|General",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "REN{0:yyMMdd}-{1:D5}",
            };

            /// <summary>
            /// How long a renewal's card authorization stays valid at the payment provider. A renewal whose order ships
            /// later is authorized again before the capture. Most card networks keep an authorization for 7 days.
            /// </summary>
            public static SettingDescriptor AuthorizationLifetimeMinutes { get; } = new()
            {
                Name = "ProductSubscription.Payments.AuthorizationLifetimeMinutes",
                GroupName = "Product Subscription|Payments",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultAuthorizationLifetimeDays * MinutesPerDay,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return SchedulerEnabled;
                    yield return SchedulerCronExpression;
                    yield return SubscriptionNumberTemplate;
                    yield return RenewalNumberTemplate;
                    yield return AuthorizationLifetimeMinutes;
                }
            }
        }

        /// <summary>Settings a merchant can set per store. Defaults follow the "strictest US rule" baseline.</summary>
        public static class Store
        {
            public static SettingDescriptor Enabled { get; } = new()
            {
                Name = "ProductSubscription.Enabled",
                GroupName = "Product Subscription|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
                IsPublic = true,
            };

            public static SettingDescriptor ReminderLeadDays { get; } = new()
            {
                Name = "ProductSubscription.ReminderLeadDays",
                GroupName = "Product Subscription|Renewals",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultReminderLeadDays,
                IsPublic = true,
            };

            public static SettingDescriptor CutoffHours { get; } = new()
            {
                Name = "ProductSubscription.CutoffHours",
                GroupName = "Product Subscription|Renewals",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultCutoffHours,
                IsPublic = true,
            };

            public static SettingDescriptor RetryScheduleHours { get; } = new()
            {
                Name = "ProductSubscription.RetryScheduleHours",
                GroupName = "Product Subscription|Payments",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "24,72,120",
            };

            public static SettingDescriptor MaxRetriesPer30Days { get; } = new()
            {
                Name = "ProductSubscription.MaxRetriesPer30Days",
                GroupName = "Product Subscription|Payments",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultMaxRetriesPer30Days,
            };

            public static SettingDescriptor PaymentActionWaitDays { get; } = new()
            {
                Name = "ProductSubscription.PaymentActionWaitDays",
                GroupName = "Product Subscription|Payments",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultPaymentActionWaitDays,
            };

            public static SettingDescriptor TermsVersion { get; } = new()
            {
                Name = "ProductSubscription.Terms.Version",
                GroupName = "Product Subscription|Terms",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "2026-11",
                IsPublic = true,
            };

            public static SettingDescriptor TermsText { get; } = new()
            {
                Name = "ProductSubscription.Terms.Text",
                GroupName = "Product Subscription|Terms",
                ValueType = SettingValueType.LongText,
                DefaultValue = DefaultTermsText,
                IsPublic = true,
            };

            public static SettingDescriptor AnnualReminderEnabled { get; } = new()
            {
                Name = "ProductSubscription.AnnualReminder.Enabled",
                GroupName = "Product Subscription|Notifications",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
            };

            /// <summary>The colour of buttons and links in subscription emails (CSS colour).</summary>
            public static SettingDescriptor EmailAccentColor { get; } = new()
            {
                Name = "ProductSubscription.Email.AccentColor",
                GroupName = "Product Subscription|Notifications",
                ValueType = SettingValueType.ShortText,
                DefaultValue = DefaultEmailAccentColor,
            };

            /// <summary>An absolute URL of the logo shown at the top of subscription emails; empty shows the store name.</summary>
            public static SettingDescriptor EmailLogoUrl { get; } = new()
            {
                Name = "ProductSubscription.Email.LogoUrl",
                GroupName = "Product Subscription|Notifications",
                ValueType = SettingValueType.ShortText,
                DefaultValue = string.Empty,
            };

            // What a customer can do with a subscription from the storefront. Each flag hides the action in the storefront
            // (it is public, and folded into the can* fields) and makes the xAPI refuse it. The back office is not limited.
            // Cancelling has no flag: US auto-renewal laws require that a customer can always cancel online.
            public static SettingDescriptor CustomerCanSkip { get; } = new()
            {
                Name = "ProductSubscription.Customer.AllowSkip",
                GroupName = "Product Subscription|Customer actions",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
                IsPublic = true,
            };

            public static SettingDescriptor CustomerCanOrderNow { get; } = new()
            {
                Name = "ProductSubscription.Customer.AllowOrderNow",
                GroupName = "Product Subscription|Customer actions",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
                IsPublic = true,
            };

            public static SettingDescriptor CustomerCanChangePaymentMethod { get; } = new()
            {
                Name = "ProductSubscription.Customer.AllowChangePaymentMethod",
                GroupName = "Product Subscription|Customer actions",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
                IsPublic = true,
            };

            public static SettingDescriptor CustomerCanPause { get; } = new()
            {
                Name = "ProductSubscription.Customer.AllowPause",
                GroupName = "Product Subscription|Customer actions",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
                IsPublic = true,
            };

            public static SettingDescriptor CustomerCanChangeFrequency { get; } = new()
            {
                Name = "ProductSubscription.Customer.AllowChangeFrequency",
                GroupName = "Product Subscription|Customer actions",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
                IsPublic = true,
            };

            public static IEnumerable<SettingDescriptor> AllStoreSettings
            {
                get
                {
                    yield return Enabled;
                    yield return ReminderLeadDays;
                    yield return CutoffHours;
                    yield return RetryScheduleHours;
                    yield return MaxRetriesPer30Days;
                    yield return PaymentActionWaitDays;
                    yield return TermsVersion;
                    yield return TermsText;
                    yield return AnnualReminderEnabled;
                    yield return EmailAccentColor;
                    yield return EmailLogoUrl;
                    yield return CustomerCanSkip;
                    yield return CustomerCanOrderNow;
                    yield return CustomerCanChangePaymentMethod;
                    yield return CustomerCanPause;
                    yield return CustomerCanChangeFrequency;
                }
            }
        }

        /// <summary>
        /// The default terms, in Markdown: one paragraph per topic with a bold lead-in. The storefront renders it as Markdown;
        /// a store's own plain text still shows as it is.
        /// </summary>
        public const string DefaultTermsText =
            "**Automatic renewal.** Subscribed items are ordered again at the interval you chose and charged to the card saved at checkout, " +
            "at the price on the order date minus your subscription discount.\n\n" +
            "**Changes and cancellation.** You can skip a delivery, change the quantity or interval, pause or cancel at any time " +
            "before the cut-off, one day before each order date, in Account › Subscriptions.\n\n" +
            "**Price changes.** If the price goes up after the reminder we email before every delivery, we ask you to review " +
            "the delivery before it is ordered.";

        public static IEnumerable<SettingDescriptor> AllSettings => General.AllGeneralSettings.Concat(Store.AllStoreSettings);
    }
}
