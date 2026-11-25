using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.ProductSubscription.Core.Models;
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
        return ProcessAsync(payload.OrderId, payload.IsNew, payload.Choices, payload.ConsentId);
    }

    public virtual Task ProcessAsync(string orderId, bool isNew)
    {
        return ProcessAsync(orderId, isNew, choices: null);
    }

    public virtual Task ProcessAsync(string orderId, bool isNew, IList<SubscriptionCartItem> choices)
    {
        return ProcessAsync(orderId, isNew, choices, consentId: null);
    }

    public virtual async Task ProcessAsync(string orderId, bool isNew, IList<SubscriptionCartItem> choices, string consentId)
    {
        // The "order created" and "payment authorized" jobs for one order can run at the same time.
        await distributedLock.ExecuteAsync($"product-subscription:order:{orderId}", _ => ProcessLockedAsync(orderId, choices, consentId), TimeSpan.FromSeconds(60));
    }

    protected virtual async Task ProcessLockedAsync(string orderId, IList<SubscriptionCartItem> choices, string consentId)
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
                // The copy taken at checkout when creating them failed; otherwise the choices still saved for the cart.
                if (choices?.Count > 0)
                {
                    await subscriptionManager.CreateFromOrderAsync(order, choices, consentId);
                }
                else
                {
                    await subscriptionManager.CreateFromOrderAsync(order);
                }

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
