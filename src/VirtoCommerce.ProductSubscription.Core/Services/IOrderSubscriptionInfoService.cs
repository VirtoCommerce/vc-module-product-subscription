using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

/// <summary>
/// The subscription side of orders: for a page of orders, which were placed by a renewal and which subscriptions their
/// lines belong to. Two indexed searches per call (subscriptions by first order, renewals by order), whatever the
/// customer's history.
/// </summary>
public interface IOrderSubscriptionInfoService
{
    /// <summary>
    /// Returns an entry for each of <paramref name="orderIds"/> that has anything to do with a subscription; other
    /// orders are left out. With <paramref name="customerId"/>, only that customer's subscriptions and renewals count.
    /// </summary>
    Task<IList<OrderSubscriptionInfo>> GetForOrdersAsync(IList<string> orderIds, string customerId = null);
}
