using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionCartItemType : ExtendableGraphType<SubscriptionCartItem>
{
    public SubscriptionCartItemType()
    {
        Name = "SubscriptionCartItem";
        Description = "The customer's choice to subscribe to a cart line.";

        Field(x => x.Id);
        Field(x => x.CartId);
        Field(x => x.LineItemId);
        Field(x => x.ProductId);
        Field(x => x.PlanId);
        Field<NonNullGraphType<StringGraphType>>("intervalUnit").Resolve(x => x.Source.IntervalUnit.ToString());
        Field(x => x.IntervalCount);
        Field<NonNullGraphType<StringGraphType>>("intervalLabel").Resolve(x => SubscriptionSchedule.Describe(x.Source.IntervalUnit, x.Source.IntervalCount));
    }
}
