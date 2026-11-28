using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.ProductSubscription.Core.Models;
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

            IList<SubscriptionCartItem> unusedChoices = null;
            string consentId = null;
            if (entry.EntryState == EntryState.Added && !string.IsNullOrEmpty(order.ShoppingCartId))
            {
                unusedChoices = await CreateSubscriptionsAsync(order);
                if (unusedChoices != null)
                {
                    consentId = await GetCheckoutConsentIdAsync(order);
                }
            }

            if (!NeedsProcessing(entry))
            {
                continue;
            }

            // Activation and renewal handling run outside the order save, so they can never fail the customer's checkout.
            var payload = AbstractTypeFactory<OrderChangedJobPayload>.TryCreateInstance();
            payload.OrderId = order.Id;
            payload.IsNew = entry.EntryState == EntryState.Added;
            payload.Choices = unusedChoices;
            payload.ConsentId = consentId;

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
    /// Whether the change can matter to subscriptions, so not every order change in the platform costs a background job:
    /// a new checkout order (subscriptions to create), or a change to the order's status, payments or shipments
    /// (a first payment that activates subscriptions, a renewal payment, a shipment that triggers the capture).
    /// </summary>
    protected virtual bool NeedsProcessing(GenericChangedEntry<CustomerOrder> entry)
    {
        var order = entry.NewEntry;
        switch (entry.EntryState)
        {
            case EntryState.Added:
                return !string.IsNullOrEmpty(order.ShoppingCartId);
            case EntryState.Modified:
                var previous = entry.OldEntry;
                return previous == null
                    || previous.Status != order.Status
                    || GetPaymentStates(previous) != GetPaymentStates(order)
                    || GetShipmentStates(previous) != GetShipmentStates(order);
            default:
                return false;
        }
    }

    private static string GetPaymentStates(CustomerOrder order)
    {
        return string.Join("|", order.InPayments?.Select(x => $"{x.Id}:{x.PaymentStatus}:{x.Status}") ?? []);
    }

    private static string GetShipmentStates(CustomerOrder order)
    {
        return string.Join("|", order.Shipments?.Select(x => $"{x.Id}:{x.Status}") ?? []);
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
    /// Returns a copy of the choices when creating them failed, for the background job to retry with; null otherwise.
    /// </summary>
    protected virtual async Task<IList<SubscriptionCartItem>> CreateSubscriptionsAsync(CustomerOrder order)
    {
        try
        {
            await distributedLock.ExecuteAsync(
                $"product-subscription:order:{order.Id}",
                _ => subscriptionManager.CreateFromOrderAsync(order),
                TimeSpan.FromSeconds(30));
            return null;
        }
        catch (Exception ex)
        {
            // Never fail checkout; the background job retries with a copy of the choices, taken before the cart
            // change deletes them.
            logger.LogError(ex, "Failed to create subscriptions for order {OrderNumber}", order.Number);
            return await CopyChoicesAsync(order);
        }
    }

    /// <summary>The consent given for the order's checkout, kept for the retry before xOrder replaces the cart's CheckoutId.</summary>
    protected virtual async Task<string> GetCheckoutConsentIdAsync(CustomerOrder order)
    {
        try
        {
            return await subscriptionManager.GetCheckoutConsentIdAsync(order.ShoppingCartId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to keep the subscription consent of order {OrderNumber} for a retry", order.Number);
            return null;
        }
    }

    protected virtual async Task<IList<SubscriptionCartItem>> CopyChoicesAsync(CustomerOrder order)
    {
        try
        {
            var choices = await subscriptionManager.GetCartChoicesAsync(order.ShoppingCartId);
            return choices?.Count > 0 ? choices : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to keep the subscription choices of order {OrderNumber} for a retry", order.Number);
            return null;
        }
    }
}
