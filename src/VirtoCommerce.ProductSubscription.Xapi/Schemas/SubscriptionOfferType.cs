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
        Field(x => x.Currency, nullable: true).Description("Code of the currency of the prices below");
        Field<MoneyType>("price")
            .Description("The product's price for the customer, after its regular discounts; null when the product has no price")
            .ResolveAsync(async context => await SubscriptionMoney.ToMoneyAsync(context, context.Source.Price, context.Source.Currency));
        Field<MoneyType>("discountAmount")
            .Description("The subscription discount per item, computed the way the cart applies it")
            .ResolveAsync(async context => await SubscriptionMoney.ToMoneyAsync(context, context.Source.DiscountAmount, context.Source.Currency));
        Field<MoneyType>("subscriptionPrice")
            .Description("The price per item with the subscription discount: what the cart charges for a subscribed item")
            .ResolveAsync(async context => await SubscriptionMoney.ToMoneyAsync(context, context.Source.SubscriptionPrice, context.Source.Currency));
    }
}
