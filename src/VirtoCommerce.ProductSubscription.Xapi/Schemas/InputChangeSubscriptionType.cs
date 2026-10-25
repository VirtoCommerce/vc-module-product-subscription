using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputChangeSubscriptionType : InputObjectGraphType
{
    public InputChangeSubscriptionType()
    {
        Name = "InputChangeProductSubscription";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
        Field<SubscriptionIntervalUnitEnum>("intervalUnit");
        Field<IntGraphType>("intervalCount");
        Field<IntGraphType>("quantity");
        Field<DateTimeGraphType>("nextOrderDate");
        Field<StringGraphType>("savedPaymentMethodId");
    }
}
