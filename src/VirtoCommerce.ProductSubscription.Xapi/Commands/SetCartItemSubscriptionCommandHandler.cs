using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SetCartItemSubscriptionCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionOfferService offerService,
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionCartItemService cartItemService)
    : IRequestHandler<SetCartItemSubscriptionCommand, SubscriptionCartItem>
{
    public virtual Task<SubscriptionCartItem> Handle(SetCartItemSubscriptionCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var cart = await cartService.GetByIdAsync(request.CartId) ?? throw new SubscriptionOperationException("Cart not found.");
        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var lineItem = cart.Items?.FirstOrDefault(x => x.Id == request.LineItemId) ?? throw new SubscriptionOperationException("Cart item not found.");
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
        criteria.LineItemId = lineItem.Id;
        criteria.Take = 1;
        var item = (await cartItemSearchService.SearchAsync(criteria)).Results.FirstOrDefault()
            ?? AbstractTypeFactory<SubscriptionCartItem>.TryCreateInstance();

        item.StoreId = cart.StoreId;
        item.CartId = cart.Id;
        item.LineItemId = lineItem.Id;
        item.ProductId = lineItem.ProductId;
        item.CustomerId = cart.CustomerId;
        item.PlanId = offer.PlanId;
        item.IntervalUnit = unit;
        item.IntervalCount = count;

        await cartItemService.SaveChangesAsync([item]);
        await TouchCartAsync(cart);

        return item;
    });

    /// <summary>Saving the cart expires xCart's cached aggregate, so the next cart query shows the subscription discount.</summary>
    protected virtual Task TouchCartAsync(ShoppingCart cart) => cartService.SaveChangesAsync([cart]);
}
