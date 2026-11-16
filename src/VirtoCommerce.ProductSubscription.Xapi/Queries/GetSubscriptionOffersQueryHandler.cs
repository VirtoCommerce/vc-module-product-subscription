using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionOffersQueryHandler(
    ISubscriptionOfferService offerService,
    ISubscriptionDiscountCalculator discountCalculator,
    IMediator mediator)
    : IQueryHandler<GetSubscriptionOffersQuery, IList<SubscriptionOffer>>
{
    public virtual async Task<IList<SubscriptionOffer>> Handle(GetSubscriptionOffersQuery request, CancellationToken cancellationToken)
    {
        var offers = (await offerService.GetOffersAsync(request.StoreId, request.ProductIds?.Take(100).ToList())).Values.ToList();

        if (offers.Count > 0)
        {
            await ApplyPricesAsync(request, offers, cancellationToken);
        }

        return offers;
    }

    /// <summary>
    /// Prices each offer the way the cart will: the product's price for this customer (as the product page shows it,
    /// with its regular discounts), less the subscription discount computed like the cart's subscription reward.
    /// </summary>
    protected virtual async Task ApplyPricesAsync(GetSubscriptionOffersQuery request, IList<SubscriptionOffer> offers, CancellationToken cancellationToken)
    {
        var prices = await LoadPricesAsync(request, offers.Select(x => x.ProductId).Distinct().ToList(), cancellationToken);

        foreach (var offer in offers)
        {
            if (!prices.TryGetValue(offer.ProductId, out var productPrice) || productPrice?.Currency == null)
            {
                continue;
            }

            var price = productPrice.ActualPrice.Amount;
            if (price <= 0)
            {
                continue;
            }

            var discount = discountCalculator.GetDiscountPerItem(offer.DiscountType, offer.DiscountValue, price, productPrice.Currency);

            offer.Currency = productPrice.Currency.Code;
            offer.Price = price;
            offer.DiscountAmount = discount;
            offer.SubscriptionPrice = price - discount;
        }
    }

    protected virtual async Task<IDictionary<string, ProductPrice>> LoadPricesAsync(GetSubscriptionOffersQuery request, IList<string> productIds, CancellationToken cancellationToken)
    {
        var query = new LoadProductsQuery
        {
            StoreId = request.StoreId,
            CurrencyCode = request.CurrencyCode,
            CultureName = request.CultureName,
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            ObjectIds = productIds,
            IncludeFields = ["price"],
        };

        var response = await mediator.Send(query, cancellationToken);

        return (response?.Products ?? [])
            .Where(x => x?.Id != null)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().AllPrices?.FirstOrDefault());
    }
}
