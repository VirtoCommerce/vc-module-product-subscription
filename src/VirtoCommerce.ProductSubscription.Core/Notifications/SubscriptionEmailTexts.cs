using System;
using System.Collections.Generic;
using System.Globalization;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Core.Notifications;

/// <summary>
/// Short texts the subscription emails compute in code, in the email's language: the delivery frequency and the
/// subscription discount. They match the storefront's own wording (product-subscriptions locales).
/// </summary>
public static class SubscriptionEmailTexts
{
    public const string DefaultLanguage = "en";

    // Two forms: "one | other". Four forms (ru, pl): "zero | one | few | many".
    private static readonly Dictionary<string, Dictionary<string, string[]>> Frequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Every day", "Every {0} days"],
            ["week"] = ["Every week", "Every {0} weeks"],
            ["month"] = ["Every month", "Every {0} months"],
            ["year"] = ["Every year", "Every {0} years"],
        },
        ["de"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Jeden Tag", "Alle {0} Tage"],
            ["week"] = ["Jede Woche", "Alle {0} Wochen"],
            ["month"] = ["Jeden Monat", "Alle {0} Monate"],
            ["year"] = ["Jedes Jahr", "Alle {0} Jahre"],
        },
        ["es"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Cada día", "Cada {0} días"],
            ["week"] = ["Cada semana", "Cada {0} semanas"],
            ["month"] = ["Cada mes", "Cada {0} meses"],
            ["year"] = ["Cada año", "Cada {0} años"],
        },
        ["fi"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Joka päivä", "{0} päivän välein"],
            ["week"] = ["Joka viikko", "{0} viikon välein"],
            ["month"] = ["Joka kuukausi", "{0} kuukauden välein"],
            ["year"] = ["Joka vuosi", "{0} vuoden välein"],
        },
        ["fr"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Tous les jours", "Tous les {0} jours"],
            ["week"] = ["Toutes les semaines", "Toutes les {0} semaines"],
            ["month"] = ["Tous les mois", "Tous les {0} mois"],
            ["year"] = ["Tous les ans", "Tous les {0} ans"],
        },
        ["it"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Ogni giorno", "Ogni {0} giorni"],
            ["week"] = ["Ogni settimana", "Ogni {0} settimane"],
            ["month"] = ["Ogni mese", "Ogni {0} mesi"],
            ["year"] = ["Ogni anno", "Ogni {0} anni"],
        },
        ["ja"] = new Dictionary<string, string[]>
        {
            ["day"] = ["毎日", "{0}日ごと"],
            ["week"] = ["毎週", "{0}週間ごと"],
            ["month"] = ["毎月", "{0}か月ごと"],
            ["year"] = ["毎年", "{0}年ごと"],
        },
        ["no"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Hver dag", "Hver {0}. dag"],
            ["week"] = ["Hver uke", "Hver {0}. uke"],
            ["month"] = ["Hver måned", "Hver {0}. måned"],
            ["year"] = ["Hvert år", "Hvert {0}. år"],
        },
        ["pl"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Co {0} dni", "Co dzień", "Co {0} dni", "Co {0} dni"],
            ["week"] = ["Co {0} tygodni", "Co tydzień", "Co {0} tygodnie", "Co {0} tygodni"],
            ["month"] = ["Co {0} miesięcy", "Co miesiąc", "Co {0} miesiące", "Co {0} miesięcy"],
            ["year"] = ["Co {0} lat", "Co rok", "Co {0} lata", "Co {0} lat"],
        },
        ["pt"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Todo dia", "A cada {0} dias"],
            ["week"] = ["Toda semana", "A cada {0} semanas"],
            ["month"] = ["Todo mês", "A cada {0} meses"],
            ["year"] = ["Todo ano", "A cada {0} anos"],
        },
        ["ru"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Раз в {0} дней", "Раз в {0} день", "Раз в {0} дня", "Раз в {0} дней"],
            ["week"] = ["Раз в {0} недель", "Раз в {0} неделю", "Раз в {0} недели", "Раз в {0} недель"],
            ["month"] = ["Раз в {0} месяцев", "Раз в {0} месяц", "Раз в {0} месяца", "Раз в {0} месяцев"],
            ["year"] = ["Раз в {0} лет", "Раз в {0} год", "Раз в {0} года", "Раз в {0} лет"],
        },
        ["sv"] = new Dictionary<string, string[]>
        {
            ["day"] = ["Varje dag", "Med {0} dagars mellanrum"],
            ["week"] = ["Varje vecka", "Med {0} veckors mellanrum"],
            ["month"] = ["Varje månad", "Med {0} månaders mellanrum"],
            ["year"] = ["Varje år", "Med {0} års mellanrum"],
        },
        ["zh"] = new Dictionary<string, string[]>
        {
            ["day"] = ["每天", "每 {0} 天"],
            ["week"] = ["每周", "每 {0} 周"],
            ["month"] = ["每月", "每 {0} 个月"],
            ["year"] = ["每年", "每 {0} 年"],
        },
    };

    private static readonly Dictionary<string, (string Percent, string Amount)> Discounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = ("Save {0}%", "Save {0}"),
        ["de"] = ("{0}% sparen", "{0} sparen"),
        ["es"] = ("Ahorra un {0} %", "Ahorra {0}"),
        ["fi"] = ("Säästä {0} %", "Säästä {0}"),
        ["fr"] = ("Économisez {0} %", "Économisez {0}"),
        ["it"] = ("Risparmia il {0}%", "Risparmia {0}"),
        ["ja"] = ("{0}%オフ", "{0}お得"),
        ["no"] = ("Spar {0} %", "Spar {0}"),
        ["pl"] = ("Oszczędzasz {0}%", "Oszczędzasz {0}"),
        ["pt"] = ("Economize {0}%", "Economize {0}"),
        ["ru"] = ("Скидка {0}%", "Скидка {0}"),
        ["sv"] = ("Spara {0} %", "Spara {0}"),
        ["zh"] = ("节省 {0}%", "节省 {0}"),
    };

    /// <summary>The two-letter language of a culture name ("de-DE" -> "de"), or English when it has no texts.</summary>
    public static string GetLanguage(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return DefaultLanguage;
        }

        var language = languageCode.Split(['-', '_'])[0].ToLowerInvariant();
        // Norwegian Bokmål stores use "nb-NO"; its texts are filed under "no".
        if (language is "nb" or "nn")
        {
            language = "no";
        }

        return Frequencies.ContainsKey(language) ? language : DefaultLanguage;
    }

    /// <summary>"Every month", "Every 2 weeks" in the given language.</summary>
    public static string Frequency(string languageCode, SubscriptionIntervalUnit unit, int count)
    {
        var language = GetLanguage(languageCode);
        var forms = Frequencies[language][unit.ToString().ToLowerInvariant()];
        var form = forms[PluralIndex(language, Math.Max(1, count), forms.Length)];
        return string.Format(CultureInfo.InvariantCulture, form, count);
    }

    /// <summary>"Save 10%" for a percent discount, "Save $5.00" for an amount (already formatted).</summary>
    public static string Discount(string languageCode, SubscriptionDiscountType type, string value)
    {
        var texts = Discounts[GetLanguage(languageCode)];
        return string.Format(CultureInfo.InvariantCulture, type == SubscriptionDiscountType.Percent ? texts.Percent : texts.Amount, value);
    }

    // The forms of a four-form message (vue-i18n's order): zero, one, few, many. Two-form messages: one, other.
    private const int FourForms = 4;
    private const int ZeroForm = 0;
    private const int OneForm = 1;
    private const int FewForm = 2;
    private const int ManyForm = 3;

    /// <summary>The form to use for <paramref name="count"/>, by the same rule as the storefront's plural messages.</summary>
    public static int PluralIndex(string language, int count, int formCount)
    {
        if (formCount < FourForms)
        {
            return count == 1 ? 0 : Math.Min(1, formCount - 1);
        }

        if (count == 0)
        {
            return ZeroForm;
        }

        return language switch
        {
            "ru" => RussianForm(count),
            "pl" => PolishForm(count),
            _ => count == 1 ? OneForm : ManyForm,
        };
    }

    // 1, 21, 101: one; 2-4, 22-24: few; the rest: many
    private static int RussianForm(int count)
    {
        if (count % 10 == 1 && count % 100 is not 11)
        {
            return OneForm;
        }

        return IsFew(count) ? FewForm : ManyForm;
    }

    // 1: one; 2-4, 22-24: few; the rest (21 too): many
    private static int PolishForm(int count)
    {
        if (count == 1)
        {
            return OneForm;
        }

        return IsFew(count) ? FewForm : ManyForm;
    }

    private static bool IsFew(int count)
    {
        return count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14;
    }
}
