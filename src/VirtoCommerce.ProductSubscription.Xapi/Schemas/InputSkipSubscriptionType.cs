using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputSkipSubscriptionType : InputObjectGraphType
{
    public InputSkipSubscriptionType()
    {
        Name = "InputSkipProductSubscription";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
        Field<DateTimeGraphType>("expectedNextOrderDate")
            .Description("The next delivery date the customer saw. If the subscription has moved on since, the skip is refused.");
    }
}
