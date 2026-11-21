using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Core;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class OrderSubscriptionInfoService(
    ISubscriptionSearchService subscriptionSearchService,
    IRenewalSearchService renewalSearchService,
    ISubscriptionService subscriptionService)
    : IOrderSubscriptionInfoService
{
    /// <summary>The most orders looked up in one batch; more are answered batch by batch.</summary>
    protected const int MaxOrders = 100;

    public virtual async Task<IList<OrderSubscriptionInfo>> GetForOrdersAsync(IList<string> orderIds, string customerId = null)
    {
        var ids = orderIds?.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList() ?? [];
        var result = new List<OrderSubscriptionInfo>();
        foreach (var batch in ids.Chunk(MaxOrders))
        {
            result.AddRange(await GetForBatchAsync(batch, customerId));
        }

        return result;
    }

    /// <summary>The subscription info of at most <see cref="MaxOrders"/> orders.</summary>
    protected virtual async Task<IList<OrderSubscriptionInfo>> GetForBatchAsync(IList<string> ids, string customerId)
    {

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
        criteria.Take = ModuleConstants.SearchPageSize;

        return await renewalSearchService.SearchAllNoCloneAsync(criteria);
    }

    protected virtual async Task<IList<Subscription>> GetStartedSubscriptionsAsync(IList<string> orderIds, string customerId)
    {
        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.InitialOrderIds = orderIds;
        criteria.CustomerId = customerId;
        criteria.Take = ModuleConstants.SearchPageSize;

        return await subscriptionSearchService.SearchAllNoCloneAsync(criteria);
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
