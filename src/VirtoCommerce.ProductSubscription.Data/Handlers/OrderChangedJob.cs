using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Handlers;

/// <summary>
/// Handles an order change in the background: creates and activates subscriptions for a new order, and hands
/// renewal orders to the renewal processor.
/// </summary>
public class OrderChangedJob(
    ICustomerOrderService customerOrderService,
    ISubscriptionManager subscriptionManager,
    IRenewalProcessor renewalProcessor,
    IDistributedLock distributedLock,
    ILogger<OrderChangedJob> logger)
    : IBackgroundJobHandler<OrderChangedJobPayload>
{
    public virtual Task Execute(OrderChangedJobPayload payload, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return ProcessAsync(payload.OrderId, payload.IsNew);
    }

    public virtual async Task ProcessAsync(string orderId, bool isNew)
    {
        // The "order created" and "payment authorized" jobs for one order can run at the same time.
        await distributedLock.ExecuteAsync($"product-subscription:order:{orderId}", _ => ProcessLockedAsync(orderId), TimeSpan.FromSeconds(60));
    }

    protected virtual async Task ProcessLockedAsync(string orderId)
    {
        var order = await customerOrderService.GetByIdAsync(orderId);
        if (order == null || order.IsPrototype)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(order.ShoppingCartId))
            {
                await subscriptionManager.CreateFromOrderAsync(order);
                await subscriptionManager.ActivateForOrderAsync(order);
            }

            await renewalProcessor.HandleOrderChangedAsync(order);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process subscriptions for order {OrderNumber}", order.Number);
            throw;
        }
    }
}
