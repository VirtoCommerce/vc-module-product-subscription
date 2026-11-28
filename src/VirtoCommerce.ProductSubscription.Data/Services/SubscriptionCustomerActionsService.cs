using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionCustomerActionsService(IStoreService storeService) : ISubscriptionCustomerActionsService
{
    public virtual async Task<SubscriptionCustomerActions> GetAsync(string storeId)
    {
        var store = string.IsNullOrEmpty(storeId) ? null : await storeService.GetNoCloneAsync(storeId);

        return new SubscriptionCustomerActions
        {
            CanSkip = GetFlag(store, ModuleConstants.Settings.Store.CustomerCanSkip),
            CanOrderNow = GetFlag(store, ModuleConstants.Settings.Store.CustomerCanOrderNow),
            CanChangePaymentMethod = GetFlag(store, ModuleConstants.Settings.Store.CustomerCanChangePaymentMethod),
            CanPause = GetFlag(store, ModuleConstants.Settings.Store.CustomerCanPause),
            CanChangeFrequency = GetFlag(store, ModuleConstants.Settings.Store.CustomerCanChangeFrequency),
        };
    }

    /// <summary>The store's value, or the setting's default when the store or its settings aren't loaded.</summary>
    protected static bool GetFlag(Store store, SettingDescriptor setting)
    {
        return store?.Settings != null ? store.Settings.GetValue<bool>(setting) : (bool)setting.DefaultValue;
    }
}
