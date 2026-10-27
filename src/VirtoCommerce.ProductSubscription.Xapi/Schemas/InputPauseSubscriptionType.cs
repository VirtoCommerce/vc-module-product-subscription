using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class InputPauseSubscriptionType : InputObjectGraphType
{
    public InputPauseSubscriptionType()
    {
        Name = "InputPauseProductSubscription";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
        Field<DateTimeGraphType>("resumeDate");
    }
}
