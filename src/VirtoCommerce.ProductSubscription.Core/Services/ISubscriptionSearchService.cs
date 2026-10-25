using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Services;

public interface ISubscriptionSearchService : ISearchService<SubscriptionSearchCriteria, SubscriptionSearchResult, Subscription>;
