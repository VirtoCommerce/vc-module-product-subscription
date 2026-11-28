using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

/// <summary>
/// Reads which subscription actions a store lets its customers take (the store's "Customer actions" settings).
/// </summary>
public interface ISubscriptionCustomerActionsService
{
    Task<SubscriptionCustomerActions> GetAsync(string storeId);
}
