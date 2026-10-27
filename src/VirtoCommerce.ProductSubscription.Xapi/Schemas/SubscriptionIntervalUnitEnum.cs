using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionIntervalUnitEnum : EnumerationGraphType<SubscriptionIntervalUnit>
{
    public SubscriptionIntervalUnitEnum()
    {
        Name = "SubscriptionIntervalUnit";
        Description = "The unit of a delivery frequency: every N days, weeks, months or years.";
    }
}
