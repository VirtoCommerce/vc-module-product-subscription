using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Core;

namespace VirtoCommerce.ProductSubscription.Data.Handlers;

/// <summary>
/// Removes subscription choices for cart lines that no longer exist, so a later one-time purchase of the same product
/// is never turned into a subscription by a stale choice. Keeps the configuration fingerprint of a configured line
/// current when the customer changes its options.
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
        criteria.Take = ModuleConstants.SearchPageSize;
        var choices = await cartItemSearchService.SearchAllNoCloneAsync(criteria);
        if (choices.Count == 0)
        {
            return;
        }

        var stale = new List<string>();
        var changed = new List<SubscriptionCartItem>();
        foreach (var choice in choices)
        {
            var (cart, deleted) = carts.First(x => x.Cart.Id == choice.CartId);
            var lineItem = deleted ? null : cart.Items?.FirstOrDefault(x => x.Id == choice.LineItemId);
            if (lineItem == null)
            {
                stale.Add(choice.Id);
            }
            else if (WithCurrentFingerprint(choice, lineItem) is { } copy)
            {
                changed.Add(copy);
            }
        }

        if (stale.Count > 0)
        {
            await cartItemService.DeleteAsync(stale);
        }

        if (changed.Count > 0)
        {
            await cartItemService.SaveChangesAsync(changed);
        }
    }

    /// <summary>A copy of the choice with the line's current configuration fingerprint, or null when it hasn't changed.</summary>
    protected static SubscriptionCartItem WithCurrentFingerprint(SubscriptionCartItem choice, LineItem lineItem)
    {
        // A cart loaded without its configuration says nothing about it: keep the stored fingerprint.
        if (lineItem.IsConfigured && lineItem.ConfigurationItems == null)
        {
            return null;
        }

        var hash = SubscriptionConfigurationMapper.GetHash(lineItem);
        if (hash == choice.ConfigurationHash)
        {
            return null;
        }

        var copy = (SubscriptionCartItem)choice.Clone();
        copy.ConfigurationHash = hash;
        return copy;
    }
}
