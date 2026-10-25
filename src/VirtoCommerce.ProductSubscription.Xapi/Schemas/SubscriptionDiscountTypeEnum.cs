using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionDiscountTypeEnum : EnumerationGraphType<SubscriptionDiscountType>
{
    public SubscriptionDiscountTypeEnum()
    {
        Name = "SubscriptionDiscountType";
        Description = "How a subscription plan discounts the price: no discount, a percent, or a fixed amount per item.";
    }
}
