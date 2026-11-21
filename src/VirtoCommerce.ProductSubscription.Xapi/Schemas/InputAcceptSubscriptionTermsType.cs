using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputAcceptSubscriptionTermsType : InputObjectGraphType
{
    public InputAcceptSubscriptionTermsType()
    {
        Name = "InputAcceptSubscriptionTerms";
        Field<NonNullGraphType<StringGraphType>>("storeId");
        Field<NonNullGraphType<StringGraphType>>("cartId");
        Field<NonNullGraphType<StringGraphType>>("termsVersion");
        Field<NonNullGraphType<StringGraphType>>("termsHash");
    }
}
