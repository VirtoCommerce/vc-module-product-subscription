using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

/// <summary>
/// Turns the module's decimal amounts into xAPI <c>MoneyType</c> values: the currency comes from the record
/// (renewal, subscription, offer) and the formatting culture from the query's <c>cultureName</c> argument,
/// falling back to the record's own language.
/// </summary>
public static class SubscriptionMoney
{
    public const string CultureNameArgument = "cultureName";

    /// <summary>Makes the query's <c>cultureName</c> argument visible to the nested money fields.</summary>
    public static void RememberCultureName(IResolveFieldContext context)
    {
        var cultureName = context.GetArgument<string>(CultureNameArgument);
        if (!string.IsNullOrEmpty(cultureName))
        {
            context.UserContext[CultureNameArgument] = cultureName;
        }
    }

    /// <summary>
    /// The amount as money in <paramref name="currencyCode"/>, or null when there is no amount or the currency is unknown.
    /// </summary>
    public static async Task<Money> ToMoneyAsync(IResolveFieldContext context, decimal? amount, string currencyCode, string fallbackCultureName = null)
    {
        if (amount == null || string.IsNullOrEmpty(currencyCode))
        {
            return null;
        }

        var currencyService = context.RequestServices?.GetService<ICurrencyService>();
        if (currencyService == null)
        {
            return null;
        }

        var currencies = (await currencyService.GetAllCurrenciesAsync()).ToList();
        if (!currencies.Any(x => x.Code.EqualsIgnoreCase(currencyCode)))
        {
            return null;
        }

        var cultureName = GetCultureName(context) ?? fallbackCultureName;
        var currency = currencies.GetCurrencyForLanguage(currencyCode, IsKnownCulture(cultureName) ? cultureName : null);

        return new Money(amount.Value, currency);
    }

    private static string GetCultureName(IResolveFieldContext context)
    {
        if (context.UserContext.TryGetValue(CultureNameArgument, out var value) && value is string cultureName && !string.IsNullOrEmpty(cultureName))
        {
            return cultureName;
        }

        return null;
    }

    private static bool IsKnownCulture(string cultureName)
    {
        if (string.IsNullOrEmpty(cultureName))
        {
            return false;
        }

        try
        {
            return CultureInfo.GetCultureInfo(cultureName) != null;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
