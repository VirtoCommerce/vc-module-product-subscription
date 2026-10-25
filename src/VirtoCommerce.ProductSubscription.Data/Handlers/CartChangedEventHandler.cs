using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Handlers;

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
