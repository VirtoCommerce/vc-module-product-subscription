using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;
using VirtoCommerce.XCart.Core.Services;
using CartAggregate = VirtoCommerce.XCart.Core.CartAggregate;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SetCartItemSubscriptionCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionOfferService offerService,
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionCartItemService cartItemService,
    ICartAggregateRepository cartAggregateRepository)
    : IRequestHandler<SetCartItemSubscriptionCommand, SubscriptionCartItem>
{
    public virtual Task<SubscriptionCartItem> Handle(SetCartItemSubscriptionCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var cart = await cartService.GetByIdAsync(request.CartId) ?? throw new SubscriptionOperationException("Cart not found.");
        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var lineItem = cart.Items?.FirstOrDefault(x => x.Id == request.LineItemId) ?? throw new SubscriptionOperationException("Cart item not found.");
        await EnsureHasOwnPriceAsync(cart, lineItem);
        var unit = request.IntervalUnit;
        var count = Math.Max(1, request.IntervalCount);

        var offers = await offerService.GetOffersAsync(cart.StoreId, [lineItem.ProductId]);
        if (!offers.TryGetValue(lineItem.ProductId, out var offer) || offer.PlanId != request.PlanId)
        {
            throw new SubscriptionOperationException("This product can't be subscribed to.");
        }

        if (!offer.Intervals.Any(x => x.Unit == unit && x.Count == count))
        {
            throw new SubscriptionOperationException("This frequency is not offered for this product.");
        }

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.Take = ModuleConstants.SearchPageSize;
        var choices = await cartItemSearchService.SearchAllNoCloneAsync(criteria);
        EnsureOnlySubscribedLinesOfProduct(cart, lineItem, choices);

        var item = (SubscriptionCartItem)choices.FirstOrDefault(x => x.LineItemId == lineItem.Id)?.Clone()
            ?? AbstractTypeFactory<SubscriptionCartItem>.TryCreateInstance();

        item.StoreId = cart.StoreId;
        item.CartId = cart.Id;
        item.LineItemId = lineItem.Id;
        item.ProductId = lineItem.ProductId;
        item.CustomerId = cart.CustomerId;
        item.ConfigurationHash = SubscriptionConfigurationMapper.GetHash(lineItem);
        item.PlanId = offer.PlanId;
        item.IntervalUnit = unit;
        item.IntervalCount = count;

        await cartItemService.SaveChangesAsync([item]);
        await TouchCartAsync(cart);

        return item;
    });

    /// <summary>
    /// A product with variations and no price of its own (the parent of the variations) can't be delivered: the customer
    /// subscribes to one of its variations. A configured line is priced from its options, so it is allowed.
    /// </summary>
    protected virtual async Task EnsureHasOwnPriceAsync(ShoppingCart cart, LineItem lineItem)
    {
        if (lineItem.IsConfigured || HasPrice(lineItem))
        {
            return;
        }

        // A zero stored price doesn't mean the product has none: xCart's updateCartQuantity (the product page's quantity
        // stepper) stores a new line before pricing it, and prices the lines only when it loads the cart. Ask xCart.
        var aggregate = await cartAggregateRepository.GetCartByIdAsync(cart.Id);
        var pricedLine = aggregate?.Cart?.Items?.FirstOrDefault(x => x.Id == lineItem.Id);
        if (pricedLine == null || !HasPrice(pricedLine))
        {
            throw new SubscriptionOperationException("This product has no price of its own. Choose one of its variations to subscribe.");
        }
    }

    protected static bool HasPrice(LineItem lineItem) => lineItem.ListPrice > 0 || lineItem.SalePrice > 0;

    /// <summary>
    /// The subscription discount is a promotion reward, and xCart gives a reward to every line of its product. A one-time
    /// line of the same product (another configuration of it) would get the discount too, so the customer subscribes to
    /// all of them or removes the others first.
    /// </summary>
    protected virtual void EnsureOnlySubscribedLinesOfProduct(ShoppingCart cart, LineItem lineItem, IList<SubscriptionCartItem> choices)
    {
        var subscribedLineIds = choices.Select(x => x.LineItemId).ToHashSet();
        var hasOneTimeLine = cart.Items
            .Where(x => x.Id != lineItem.Id && x.ProductId == lineItem.ProductId)
            .Any(x => x.SelectedForCheckout && !x.IsGift && !subscribedLineIds.Contains(x.Id));

        if (hasOneTimeLine)
        {
            throw new SubscriptionOperationException("This product is in your cart more than once. Subscribe to each of its lines, or remove the others, to subscribe to this one.");
        }
    }

    /// <summary>
    /// Expires xCart's cached cart, so the next cart query is recalculated with the subscription discount. The cart
    /// itself isn't saved: saving a copy loaded earlier would undo a change made to the cart in the meantime.
    /// </summary>
    protected virtual Task TouchCartAsync(ShoppingCart cart)
    {
        GenericCachingRegion<CartAggregate>.ExpireTokenForKey(cart.Id);
        return Task.CompletedTask;
    }
}
