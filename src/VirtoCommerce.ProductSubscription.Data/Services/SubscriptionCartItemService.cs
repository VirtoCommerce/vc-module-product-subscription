using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Models;
using VirtoCommerce.ProductSubscription.Data.Repositories;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionCartItemService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher)
    : CrudService<SubscriptionCartItem, SubscriptionCartItemEntity, SubscriptionCartItemChangingEvent, SubscriptionCartItemChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
        ISubscriptionCartItemService
{
    protected override Task<IList<SubscriptionCartItemEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IProductSubscriptionRepository)repository).GetCartItemsByIdsAsync(ids, responseGroup);
    }
}
