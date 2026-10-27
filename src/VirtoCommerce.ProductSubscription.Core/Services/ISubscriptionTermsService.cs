using System.Threading.Tasks;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

/// <summary>
/// Store-level subscription terms (auto-renewal disclosure) the customer agrees to at checkout.
/// </summary>
public interface ISubscriptionTermsService
{
    Task<SubscriptionTerms> GetTermsAsync(string storeId, string languageCode = null);
}
