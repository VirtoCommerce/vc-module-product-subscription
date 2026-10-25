using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputSavedPaymentMethodIdType : InputObjectGraphType
{
    public InputSavedPaymentMethodIdType()
    {
        Name = "InputSavedPaymentMethodId";
        Field<NonNullGraphType<StringGraphType>>("id");
    }
}
