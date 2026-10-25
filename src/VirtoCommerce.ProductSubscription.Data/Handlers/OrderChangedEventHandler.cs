using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Handlers;

/// <summary>
/// Reacts to order changes without touching the Orders module:
/// creates subscriptions from a new order, activates them when the first payment is authorized,
/// and hands renewal orders to the renewal processor (customer completed a payment, shipment shipped → capture).
/// </summary>
public class OrderChangedEventHandler(
    ISubscriptionManager subscriptionManager,
    IDistributedLock distributedLock,
    ILogger<OrderChangedEventHandler> logger)
    : IEventHandler<OrderChangedEvent>
{
    public virtual async Task Handle(OrderChangedEvent message)
    {
        foreach (var entry in message.ChangedEntries)
        {
            var order = entry.NewEntry;
            if (order == null || order.IsPrototype || string.IsNullOrEmpty(order.Id))
            {
                continue;
            }

            if (entry.EntryState == EntryState.Added && !string.IsNullOrEmpty(order.ShoppingCartId))
            {
                await CreateSubscriptionsAsync(order);
            }

            // Activation and renewal handling run outside the order save, so they can never fail the customer's checkout.
            var payload = AbstractTypeFactory<OrderChangedJobPayload>.TryCreateInstance();
            payload.OrderId = order.Id;
            payload.IsNew = entry.EntryState == EntryState.Added;

            try
            {
                await EnqueueAsync(payload);
            }
            catch (Exception ex)
            {
                // Never fail the order save, e.g. when no background job engine is installed.
                logger.LogError(ex, "Failed to enqueue subscription processing for order {OrderNumber}", order.Number);
            }
        }
    }

    /// <summary>
    /// Uses the static facade, not an injected <see cref="IBackgroundJob"/>: the event handler is resolved once, from
    /// the root provider, and the facade is scoped.
    /// </summary>
    protected virtual Task EnqueueAsync(OrderChangedJobPayload payload)
    {
        return BackgroundJob.Enqueue<OrderChangedJob>(payload);
    }

    /// <summary>
    /// Runs inline: right after the order is saved, xCart removes the ordered lines from the cart, and the cart change
    /// deletes their subscription choices. Creating the subscriptions here, before that, keeps the choices available.
    /// </summary>
    protected virtual async Task CreateSubscriptionsAsync(CustomerOrder order)
    {
        try
        {
            await distributedLock.ExecuteAsync(
                $"product-subscription:order:{order.Id}",
                _ => subscriptionManager.CreateFromOrderAsync(order),
                TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            // Never fail checkout; the background job retries from whatever choices are left.
            logger.LogError(ex, "Failed to create subscriptions for order {OrderNumber}", order.Number);
        }
    }
}
