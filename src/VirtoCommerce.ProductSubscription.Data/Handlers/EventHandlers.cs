using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Handlers;

/// <summary>
/// Reacts to order changes without touching the Orders module:
/// creates subscriptions from a new order, activates them when the first payment is authorized,
/// and hands renewal orders to the renewal processor (customer completed a payment, shipment shipped → capture).
/// </summary>
public class OrderChangedEventHandler(
    IBackgroundJobClient backgroundJobClient,
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
            backgroundJobClient.Enqueue<OrderChangedJob>(x => x.ProcessAsync(order.Id, entry.EntryState == EntryState.Added));
        }
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

public class OrderChangedJob(
    ICustomerOrderService customerOrderService,
    ISubscriptionManager subscriptionManager,
    IRenewalProcessor renewalProcessor,
    IDistributedLock distributedLock,
    ILogger<OrderChangedJob> logger)
{
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

/// <summary>
/// Removes subscription choices for cart lines that no longer exist, so a later one-time purchase of the same product
/// is never turned into a subscription by a stale choice.
/// </summary>
public class CartChangedEventHandler(
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionCartItemService cartItemService)
    : IEventHandler<CartChangedEvent>
{
    public virtual async Task Handle(CartChangedEvent message)
    {
        var carts = message.ChangedEntries
            .Where(x => x.EntryState is EntryState.Modified or EntryState.Deleted && x.NewEntry != null)
            .Select(x => (Cart: x.NewEntry, Deleted: x.EntryState == EntryState.Deleted))
            .ToList();

        if (carts.Count == 0)
        {
            return;
        }

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = carts.Select(x => x.Cart.Id).Where(x => x != null).Distinct().ToList();
        criteria.Take = 1000;
        var choices = (await cartItemSearchService.SearchAsync(criteria)).Results;
        if (choices.Count == 0)
        {
            return;
        }

        var stale = new List<string>();
        foreach (var choice in choices)
        {
            var (cart, deleted) = carts.First(x => x.Cart.Id == choice.CartId);
            var lineExists = !deleted && cart.Items?.Any(x => x.Id == choice.LineItemId) == true;
            if (!lineExists)
            {
                stale.Add(choice.Id);
            }
        }

        if (stale.Count > 0)
        {
            await cartItemService.DeleteAsync(stale);
        }
    }
}
