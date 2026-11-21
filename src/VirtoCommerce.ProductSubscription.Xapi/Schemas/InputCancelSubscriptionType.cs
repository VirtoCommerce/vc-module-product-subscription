using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputCancelSubscriptionType : InputObjectGraphType
{
    public InputCancelSubscriptionType()
    {
        Name = "InputCancelProductSubscription";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
        Field<StringGraphType>("reason");
    }
}
