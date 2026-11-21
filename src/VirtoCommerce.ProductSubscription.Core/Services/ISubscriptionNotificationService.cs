using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;

namespace VirtoCommerce.ProductSubscription.Core.Services;

public interface ISubscriptionNotificationService
{
    Task SendAsync<T>(IList<Subscription> subscriptions, Renewal renewal = null, Action<T> configure = null)
        where T : SubscriptionEmailNotificationBase;

    Task<string> GetStoreUrlAsync(string storeId);
}
