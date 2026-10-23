using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.NotificationsModule.Core.Extensions;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public interface ISubscriptionNotificationService
{
    Task SendAsync<T>(IList<Subscription> subscriptions, Renewal renewal = null, Action<T> configure = null)
        where T : SubscriptionEmailNotificationBase;

    Task<string> GetStoreUrlAsync(string storeId);
}

public class SubscriptionNotificationService(
    INotificationSearchService notificationSearchService,
    INotificationSender notificationSender,
    IStoreService storeService,
    IMemberService memberService,
    Func<UserManager<ApplicationUser>> userManagerFactory,
    ILogger<SubscriptionNotificationService> logger)
    : ISubscriptionNotificationService
{
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

            var store = await storeService.GetNoCloneAsync(storeId, StoreResponseGroup.StoreInfo.ToString());
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
            var email = renewal?.CustomerEmail ?? first?.CustomerEmail ?? await GetCustomerEmailAsync(customerId);
            if (string.IsNullOrEmpty(email))
            {
                logger.LogInformation("Skipped {Notification}: no email for customer {CustomerId}", typeof(T).Name, customerId);
                return;
            }

            var storeUrl = GetStoreUrl(store);

            notification.Subscriptions = subscriptions ?? new List<Subscription>();
            notification.Renewal = renewal;
            notification.CustomerName = renewal?.CustomerName ?? first?.CustomerName;
            notification.StoreName = store.Name;
            notification.StoreUrl = storeUrl;
            notification.ManageUrl = $"{storeUrl}account/subscriptions";
            notification.From = store.EmailWithName ?? store.Email;
            notification.To = email;
            notification.LanguageCode = renewal?.LanguageCode ?? first?.LanguageCode ?? store.DefaultLanguage;
            notification.TenantIdentity = renewal != null
                ? new TenantIdentity(renewal.Id, nameof(Renewal))
                : new TenantIdentity(first?.Id, nameof(Subscription));

            configure?.Invoke(notification);

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
}
