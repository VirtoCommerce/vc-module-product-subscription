using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionIntervalType : ExtendableGraphType<SubscriptionPlanInterval>
{
    public SubscriptionIntervalType()
    {
        Name = "SubscriptionInterval";
        Description = "A delivery frequency offered for a product, e.g. every 2 weeks.";

        Field<NonNullGraphType<SubscriptionIntervalUnitEnum>>("unit").Resolve(x => x.Source.Unit);
        Field(x => x.Count).Description("Number of units between deliveries");
        Field(x => x.IsDefault).Description("Preselected frequency");
        Field<NonNullGraphType<StringGraphType>>("label").Resolve(x => SubscriptionSchedule.Describe(x.Source.Unit, x.Source.Count));
    }
}
