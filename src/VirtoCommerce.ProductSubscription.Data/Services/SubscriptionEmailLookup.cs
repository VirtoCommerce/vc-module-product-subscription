using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Data.Services;

/// <summary>What the subscription emails look up beyond the subscription itself: the store currency and the saved card.</summary>
public class SubscriptionEmailLookup(ICurrencyService currencyService, IStoredPaymentMethodService storedPaymentMethodService)
{
    /// <summary>The currency, formatted for the language; null when the platform doesn't know it.</summary>
    public virtual async Task<Currency> GetCurrencyAsync(string currencyCode, string cultureName)
    {
        if (string.IsNullOrEmpty(currencyCode))
        {
            return null;
        }

        var currencies = (await currencyService.GetAllCurrenciesAsync()).ToList();
        if (!currencies.Any(x => x.Code.EqualsIgnoreCase(currencyCode)))
        {
            return null;
        }

        return currencies.GetCurrencyForLanguage(currencyCode, cultureName);
    }

    /// <summary>"Visa •••• 4242", or empty when the card is unknown.</summary>
    public virtual async Task<string> GetCardTextAsync(string storedPaymentMethodId)
    {
        if (string.IsNullOrEmpty(storedPaymentMethodId))
        {
            return string.Empty;
        }

        var card = await storedPaymentMethodService.GetNoCloneAsync(storedPaymentMethodId);
        if (card == null || string.IsNullOrEmpty(card.Last4))
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(card.Brand) ? $"•••• {card.Last4}" : $"{card.Brand} •••• {card.Last4}";
    }
}
