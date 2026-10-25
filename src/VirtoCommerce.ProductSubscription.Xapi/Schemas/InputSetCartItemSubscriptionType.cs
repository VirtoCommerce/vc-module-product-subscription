using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputSetCartItemSubscriptionType : InputObjectGraphType
{
    public InputSetCartItemSubscriptionType()
    {
        Name = "InputSetCartItemSubscription";
        Field<NonNullGraphType<StringGraphType>>("storeId");
        Field<NonNullGraphType<StringGraphType>>("cartId");
        Field<NonNullGraphType<StringGraphType>>("lineItemId");
        Field<NonNullGraphType<StringGraphType>>("planId");
        Field<NonNullGraphType<SubscriptionIntervalUnitEnum>>("intervalUnit");
        Field<NonNullGraphType<IntGraphType>>("intervalCount");
    }
}
