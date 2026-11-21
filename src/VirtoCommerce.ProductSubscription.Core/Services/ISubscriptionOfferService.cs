using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

/// <summary>
/// Finds the subscription plan that applies to each product (directly or through its category).
/// </summary>
public interface ISubscriptionOfferService
{
    Task<IDictionary<string, SubscriptionOffer>> GetOffersAsync(string storeId, IList<string> productIds);
}
