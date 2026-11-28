using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PaymentModule.Core.Events;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Handlers;

/// <summary>
/// Links a card saved with a checkout order to the order's subscriptions. The card is saved after the payment is
/// authorized, so the subscriptions can already be activated, with the customer's default card, by then.
/// </summary>
public class StoredPaymentMethodChangedEventHandler(
    ISubscriptionManager subscriptionManager,
    ILogger<StoredPaymentMethodChangedEventHandler> logger)
    : IEventHandler<StoredPaymentMethodChangedEvent>
{
    public virtual async Task Handle(StoredPaymentMethodChangedEvent message)
    {
        foreach (var entry in message.ChangedEntries)
        {
            if (entry.EntryState != EntryState.Added || string.IsNullOrEmpty(entry.NewEntry?.InitialOrderId))
            {
                continue;
            }

            try
            {
                await subscriptionManager.LinkStoredPaymentMethodAsync(entry.NewEntry);
            }
            catch (Exception ex)
            {
                // Never fail saving the card; the subscriptions keep the card they have.
                logger.LogError(ex, "Failed to link the card saved with order {OrderId} to its subscriptions", entry.NewEntry.InitialOrderId);
            }
        }
    }
}
