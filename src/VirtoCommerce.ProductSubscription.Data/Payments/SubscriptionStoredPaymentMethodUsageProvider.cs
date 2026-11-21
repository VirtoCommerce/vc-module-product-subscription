using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Payments;

/// <summary>
/// A card that pays a subscription can't be removed: the customer chooses another card for those subscriptions first.
/// </summary>
public class SubscriptionStoredPaymentMethodUsageProvider(ISubscriptionSearchService subscriptionSearchService) : IStoredPaymentMethodUsageProvider
{
    public virtual async Task<StoredPaymentMethodUsage> GetUsageAsync(StoredPaymentMethod storedPaymentMethod)
    {
        if (string.IsNullOrEmpty(storedPaymentMethod?.Id))
        {
            return StoredPaymentMethodUsage.NotInUse;
        }

        var criteria = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        criteria.StoredPaymentMethodId = storedPaymentMethod.Id;
        criteria.Statuses = [SubscriptionStatus.Active, SubscriptionStatus.Paused, SubscriptionStatus.PastDue, SubscriptionStatus.PendingActivation];
        criteria.Take = 0;

        var count = (await subscriptionSearchService.SearchAsync(criteria)).TotalCount;
        return count switch
        {
            0 => StoredPaymentMethodUsage.NotInUse,
            1 => StoredPaymentMethodUsage.InUse("This card pays for a subscription. Choose another card for it first."),
            _ => StoredPaymentMethodUsage.InUse($"This card pays for {count} subscriptions. Choose another card for them first."),
        };
    }
}
