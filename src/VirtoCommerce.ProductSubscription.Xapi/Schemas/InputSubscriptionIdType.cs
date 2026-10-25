using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputSubscriptionIdType : InputObjectGraphType
{
    public InputSubscriptionIdType()
    {
        Name = "InputProductSubscriptionId";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
    }
}
