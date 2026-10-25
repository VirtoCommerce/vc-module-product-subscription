using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;
using CustomerOrder = VirtoCommerce.OrdersModule.Core.Model.CustomerOrder;

namespace VirtoCommerce.ProductSubscription.Data.Renewals;

/// <summary>
/// The calculation adapter: builds a server-side cart for a renewal through xCart's CartAggregate, so renewals are
/// priced, discounted, taxed, shipped and stock-validated exactly like checkout, and converts it to an order the same way.
/// </summary>
public interface IRenewalCartBuilder
{
    Task<RenewalCartResult> BuildAsync(Renewal renewal, IList<Subscription> subscriptions);
    Task<CustomerOrder> PlaceOrderAsync(Renewal renewal, RenewalCartResult cartResult);
}
