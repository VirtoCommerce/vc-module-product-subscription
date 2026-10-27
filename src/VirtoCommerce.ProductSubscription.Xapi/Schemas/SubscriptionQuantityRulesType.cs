using GraphQL.Types;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionQuantityRulesType : ObjectGraphType<SubscriptionQuantityRules>
{
    public SubscriptionQuantityRulesType()
    {
        Name = "SubscriptionQuantityRules";
        Field(x => x.PackSize).Description("The quantity must be a multiple of this.");
        Field(x => x.MinQuantity);
        Field(x => x.MaxQuantity, nullable: true);
    }
}
