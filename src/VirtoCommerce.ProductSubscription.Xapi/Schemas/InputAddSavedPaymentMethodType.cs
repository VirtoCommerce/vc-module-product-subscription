using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputAddSavedPaymentMethodType : InputObjectGraphType
{
    public InputAddSavedPaymentMethodType()
    {
        Name = "InputAddSavedPaymentMethod";
        Field<NonNullGraphType<StringGraphType>>("storeId");
        Field<NonNullGraphType<StringGraphType>>("paymentMethodCode");
        Field<ListGraphType<InputKeyValueType>>("parameters");
        Field<BooleanGraphType>("setDefault");
    }
}
