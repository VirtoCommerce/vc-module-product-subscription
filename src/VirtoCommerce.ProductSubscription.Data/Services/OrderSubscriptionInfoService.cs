using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class OrderSubscriptionInfoService(
    ISubscriptionSearchService subscriptionSearchService,
    IRenewalSearchService renewalSearchService,
    ISubscriptionService subscriptionService)
    : IOrderSubscriptionInfoService
{
    /// <summary>The most orders answered in one call: a page of order history.</summary>
    protected const int MaxOrders = 100;

    public virtual async Task<IList<OrderSubscriptionInfo>> GetForOrdersAsync(IList<string> orderIds, string customerId = null)
    {
        var ids = orderIds?.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(MaxOrders).ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        var renewals = await GetRenewalsAsync(ids, customerId);
        var started = await GetStartedSubscriptionsAsync(ids, customerId);

        // The renewals name their subscriptions; load the ones not already found by their first order.
        var known = started.ToDictionary(x => x.Id);
        var missingIds = renewals
            .SelectMany(x => x.Items.Where(i => !i.IsExcluded).Select(i => i.SubscriptionId))
            .Where(x => !string.IsNullOrEmpty(x) && !known.ContainsKey(x))
            .Distinct()
            .ToList();
        if (missingIds.Count > 0)
        {
            foreach (var subscription in await subscriptionService.GetNoCloneAsync(missingIds))
            {
                known[subscription.Id] = subscription;
            }
        }

        var result = new Dictionary<string, OrderSubscriptionInfo>();

        foreach (var renewal in renewals)
        {
            var info = GetOrAdd(result, renewal.OrderId);
            info.RenewalId = renewal.Id;
            info.RenewalNumber = renewal.Number;
            info.RenewalScheduledDate = renewal.ScheduledDate;
            info.IsManualRenewal = renewal.IsManual;

            foreach (var item in renewal.Items.Where(x => !x.IsExcluded))
            {
                if (known.TryGetValue(item.SubscriptionId, out var subscription))
                {
                    AddLine(info, subscription, item.ProductId, isStartedByOrder: false);
                }
            }
        }

        foreach (var subscription in started)
        {
            AddLine(GetOrAdd(result, subscription.InitialOrderId), subscription, subscription.ProductId, isStartedByOrder: true);
        }

        return ids.Where(result.ContainsKey).Select(x => result[x]).ToList();
    }

    protected virtual async Task<IList<Renewal>> GetRenewalsAsync(IList<string> orderIds, string customerId)
    {
        var criteria = AbstractTypeFactory<RenewalSearchCriteria>.TryCreateInstance();
        criteria.OrderIds = orderIds;
        criteria.CustomerId = customerId;
        // A renewal places one order; a few spare rows cover a re-placed order.
        criteria.Take = orderIds.Count * 2;

        return (await renewalSearchService.SearchNoCloneAsync(criteria)).Results;
    }

    protected virtual async Task<IList<Subscription>> GetStartedSubscriptionsAsync(IList<string> orderIds, string customerId)
    {
        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.InitialOrderIds = orderIds;
        criteria.CustomerId = customerId;
        // Every line of a checkout can start a subscription.
        criteria.Take = 1000;

        return (await subscriptionSearchService.SearchNoCloneAsync(criteria)).Results;
    }

    protected static OrderSubscriptionInfo GetOrAdd(IDictionary<string, OrderSubscriptionInfo> result, string orderId)
    {
        if (!result.TryGetValue(orderId, out var info))
        {
            info = AbstractTypeFactory<OrderSubscriptionInfo>.TryCreateInstance();
            info.OrderId = orderId;
            result[orderId] = info;
        }

        return info;
    }

    protected virtual void AddLine(OrderSubscriptionInfo info, Subscription subscription, string productId, bool isStartedByOrder)
    {
        if (info.Subscriptions.Any(x => x.SubscriptionId == subscription.Id))
        {
            return;
        }

        var line = AbstractTypeFactory<OrderSubscriptionLine>.TryCreateInstance();
        line.SubscriptionId = subscription.Id;
        line.SubscriptionNumber = subscription.Number;
        line.ProductId = productId ?? subscription.ProductId;
        line.IntervalUnit = subscription.IntervalUnit;
        line.IntervalCount = subscription.IntervalCount;
        line.Status = subscription.Status;
        line.IsStartedByOrder = isStartedByOrder;
        info.Subscriptions.Add(line);
    }
}
