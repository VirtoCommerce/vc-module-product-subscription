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

public class RenewalService(
    Func<IProductSubscriptionRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher,
    IUniqueNumberGenerator uniqueNumberGenerator,
    ISettingsManager settingsManager)
    : CrudService<Renewal, RenewalEntity, RenewalChangingEvent, RenewalChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
        IRenewalService
{
    protected override Task<IList<RenewalEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IProductSubscriptionRepository)repository).GetRenewalsByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<Renewal> models)
    {
        var withoutNumber = models.Where(x => string.IsNullOrEmpty(x.Number)).ToList();
        if (withoutNumber.Count > 0)
        {
            var template = await settingsManager.GetValueAsync<string>(ModuleConstants.Settings.General.RenewalNumberTemplate);
            foreach (var model in withoutNumber)
            {
                model.Number = uniqueNumberGenerator.GenerateNumber(template);
            }
        }

        foreach (var model in models.Where(x => string.IsNullOrEmpty(x.IdempotencyKey)))
        {
            model.IdempotencyKey = Guid.NewGuid().ToString("N");
        }

        await base.BeforeSaveChanges(models);
    }
}
