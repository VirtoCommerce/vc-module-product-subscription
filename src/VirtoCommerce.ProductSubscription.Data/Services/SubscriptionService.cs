using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Events;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Models;
using VirtoCommerce.ProductSubscription.Data.Repositories;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher,
    IUniqueNumberGenerator uniqueNumberGenerator,
    ISettingsManager settingsManager)
    : CrudService<Subscription, SubscriptionEntity, SubscriptionChangingEvent, SubscriptionChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
        ISubscriptionService
{
    protected override Task<IList<SubscriptionEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IProductSubscriptionRepository)repository).GetSubscriptionsByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<Subscription> models)
    {
        var withoutNumber = models.Where(x => string.IsNullOrEmpty(x.Number)).ToList();
        if (withoutNumber.Count > 0)
        {
            var template = await settingsManager.GetValueAsync<string>(ModuleConstants.Settings.General.SubscriptionNumberTemplate);
            foreach (var model in withoutNumber)
            {
                model.Number = uniqueNumberGenerator.GenerateNumber(template);
            }
        }

        await base.BeforeSaveChanges(models);
    }
}
