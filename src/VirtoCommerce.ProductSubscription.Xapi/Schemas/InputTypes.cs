using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;

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
        Field<NonNullGraphType<StringGraphType>>("intervalUnit");
        Field<NonNullGraphType<IntGraphType>>("intervalCount");
    }
}

public class InputRemoveCartItemSubscriptionType : InputObjectGraphType
{
    public InputRemoveCartItemSubscriptionType()
    {
        Name = "InputRemoveCartItemSubscription";
        Field<NonNullGraphType<StringGraphType>>("cartId");
        Field<NonNullGraphType<StringGraphType>>("lineItemId");
    }
}

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

public class InputSubscriptionIdType : InputObjectGraphType
{
    public InputSubscriptionIdType()
    {
        Name = "InputProductSubscriptionId";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
    }
}

public class InputPauseSubscriptionType : InputObjectGraphType
{
    public InputPauseSubscriptionType()
    {
        Name = "InputPauseProductSubscription";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
        Field<DateTimeGraphType>("resumeDate");
    }
}

public class InputCancelSubscriptionType : InputObjectGraphType
{
    public InputCancelSubscriptionType()
    {
        Name = "InputCancelProductSubscription";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
        Field<StringGraphType>("reason");
    }
}

public class InputChangeSubscriptionType : InputObjectGraphType
{
    public InputChangeSubscriptionType()
    {
        Name = "InputChangeProductSubscription";
        Field<NonNullGraphType<StringGraphType>>("subscriptionId");
        Field<StringGraphType>("intervalUnit");
        Field<IntGraphType>("intervalCount");
        Field<IntGraphType>("quantity");
        Field<DateTimeGraphType>("nextOrderDate");
        Field<StringGraphType>("savedPaymentMethodId");
    }
}

public class InputRenewalIdType : InputObjectGraphType
{
    public InputRenewalIdType()
    {
        Name = "InputSubscriptionRenewalId";
        Field<NonNullGraphType<StringGraphType>>("renewalId");
    }
}

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

public class InputSavedPaymentMethodIdType : InputObjectGraphType
{
    public InputSavedPaymentMethodIdType()
    {
        Name = "InputSavedPaymentMethodId";
        Field<NonNullGraphType<StringGraphType>>("id");
    }
}
