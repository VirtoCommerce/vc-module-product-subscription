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

            public static SettingDescriptor MockAuthorizationLifetimeMinutes { get; } = new()
            {
                Name = "ProductSubscription.MockPayment.AuthorizationLifetimeMinutes",
                GroupName = "Product Subscription|Mock payment method",
                ValueType = SettingValueType.Integer,
                DefaultValue = 7 * 24 * 60,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return SchedulerEnabled;
                    yield return SchedulerCronExpression;
                    yield return SubscriptionNumberTemplate;
                    yield return RenewalNumberTemplate;
                    yield return MockAuthorizationLifetimeMinutes;
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
                DefaultValue = 3,
                IsPublic = true,
            };

            public static SettingDescriptor CutoffHours { get; } = new()
            {
                Name = "ProductSubscription.CutoffHours",
                GroupName = "Product Subscription|Renewals",
                ValueType = SettingValueType.Integer,
                DefaultValue = 24,
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
                DefaultValue = 15,
            };

            public static SettingDescriptor PaymentActionWaitDays { get; } = new()
            {
                Name = "ProductSubscription.PaymentActionWaitDays",
                GroupName = "Product Subscription|Payments",
                ValueType = SettingValueType.Integer,
                DefaultValue = 7,
            };

            public static SettingDescriptor TermsVersion { get; } = new()
            {
                Name = "ProductSubscription.Terms.Version",
                GroupName = "Product Subscription|Terms",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "2026-10",
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
                }
            }
        }

        public const string DefaultTermsText =
            "Your subscription renews automatically at the frequency you chose until you cancel. " +
            "Each delivery is charged to your saved payment method at the price shown in the reminder we email before every order, " +
            "including the subscription discount, shipping and tax. If the price goes up, we ask you to confirm before we charge more. " +
            "You can skip, pause, change or cancel any time in My account › Subscriptions; cancelling stops all future charges right away.";

        public static IEnumerable<SettingDescriptor> AllSettings => General.AllGeneralSettings.Concat(Store.AllStoreSettings);
    }
}
