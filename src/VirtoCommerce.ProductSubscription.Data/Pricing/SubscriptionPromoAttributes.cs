using System.Collections.Generic;
using System.Globalization;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Pricing;

public static class SubscriptionPromoAttributes
{
    public const string PlanId = "productSubscription.planId";
    public const string PlanName = "productSubscription.planName";
    public const string DiscountType = "productSubscription.discountType";
    public const string DiscountValue = "productSubscription.discountValue";

    public static void Set(ProductPromoEntry entry, SubscriptionLineDiscount discount)
    {
        entry.Attributes ??= new Dictionary<string, string>();
        entry.Attributes[PlanId] = discount.PlanId;
        entry.Attributes[PlanName] = discount.PlanName;
        entry.Attributes[DiscountType] = discount.DiscountType.ToString();
        entry.Attributes[DiscountValue] = discount.DiscountValue.ToString(CultureInfo.InvariantCulture);
    }

    public static SubscriptionLineDiscount Get(ProductPromoEntry entry)
    {
        if (entry?.Attributes == null || !entry.Attributes.TryGetValue(DiscountType, out var type))
        {
            return null;
        }

        entry.Attributes.TryGetValue(PlanId, out var planId);
        entry.Attributes.TryGetValue(PlanName, out var planName);
        entry.Attributes.TryGetValue(DiscountValue, out var value);

        return new SubscriptionLineDiscount(
            planId,
            planName,
            EnumUtility.SafeParse(type, SubscriptionDiscountType.None),
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m);
    }
}
