using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputRenewalIdType : InputObjectGraphType
{
    public InputRenewalIdType()
    {
        Name = "InputSubscriptionRenewalId";
        Field<NonNullGraphType<StringGraphType>>("renewalId");
    }
}
