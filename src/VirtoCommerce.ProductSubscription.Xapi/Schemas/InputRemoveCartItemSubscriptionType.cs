using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputRemoveCartItemSubscriptionType : InputObjectGraphType
{
    public InputRemoveCartItemSubscriptionType()
    {
        Name = "InputRemoveCartItemSubscription";
        Field<NonNullGraphType<StringGraphType>>("cartId");
        Field<NonNullGraphType<StringGraphType>>("lineItemId");
    }
}
