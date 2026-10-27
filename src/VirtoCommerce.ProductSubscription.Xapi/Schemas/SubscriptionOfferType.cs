using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionOfferType : ExtendableGraphType<SubscriptionOffer>
{
    public SubscriptionOfferType()
    {
        Name = "SubscriptionOffer";
        Description = "Subscribe & Save offer for a product.";

        Field(x => x.ProductId);
        Field(x => x.PlanId);
        Field(x => x.PlanName);
        Field(x => x.Description, nullable: true);
        Field<NonNullGraphType<SubscriptionDiscountTypeEnum>>("discountType").Resolve(x => x.Source.DiscountType);
        Field(x => x.DiscountValue);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionIntervalType>>>>("intervals").Resolve(x => x.Source.Intervals);
    }
}
