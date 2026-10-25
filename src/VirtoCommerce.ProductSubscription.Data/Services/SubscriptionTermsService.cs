using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.ProductSubscription.Data.Services;

public class SubscriptionTermsService(IStoreService storeService) : ISubscriptionTermsService
{
    public virtual async Task<SubscriptionTerms> GetTermsAsync(string storeId, string languageCode = null)
    {
        var store = await storeService.GetNoCloneAsync(storeId);

        var version = store?.Settings.GetValue<string>(ModuleConstants.Settings.Store.TermsVersion) ?? (string)ModuleConstants.Settings.Store.TermsVersion.DefaultValue;
        var text = store?.Settings.GetValue<string>(ModuleConstants.Settings.Store.TermsText);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = ModuleConstants.Settings.DefaultTermsText;
        }

        return new SubscriptionTerms
        {
            Version = version,
            Text = text,
            Hash = ComputeHash(text),
        };
    }

    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return System.Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
