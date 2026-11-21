using GraphQL;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class RenewalItemType : ExtendableGraphType<RenewalItem>
{
    public RenewalItemType()
    {
        Name = "SubscriptionRenewalItem";

        Field(x => x.SubscriptionId);
        Field(x => x.ProductId);
        Field(x => x.Sku, nullable: true);
        Field(x => x.ProductName, nullable: true);
        Field(x => x.ImageUrl, nullable: true);
        Field(x => x.Quantity);
        Field<SubscriptionConfigurationType>("configuration")
            .Description("For a configurable product: the configuration this delivery orders; sections that can't be ordered any more have isAvailable = false.")
            .Resolve(x => x.Source.Configuration);
        Field(x => x.ConfigurationHash, nullable: true);
        Field(x => x.IsConfigurationChanged).Description("True when the configuration can't be ordered as it is any more");
        Field(x => x.ListPrice);
        Field(x => x.DiscountAmount);
        Field(x => x.Total);
        Field<MoneyType>("totalMoney")
            .Description("Total as money, formatted in the renewal's currency")
            .ResolveAsync(async context =>
            {
                var renewal = GetRenewal(context);
                return await SubscriptionMoney.ToMoneyAsync(context, context.Source.Total, renewal?.Currency, renewal?.LanguageCode);
            });
        Field(x => x.IsAvailable);
        Field(x => x.IsExcluded);
        Field(x => x.Message, nullable: true);
    }

    /// <summary>The renewal the item belongs to: the nearest parent field whose source is a renewal.</summary>
    protected static Renewal GetRenewal(IResolveFieldContext context)
    {
        for (var parent = context.Parent; parent != null; parent = parent.Parent)
        {
            if (parent.Source is Renewal renewal)
            {
                return renewal;
            }
        }

        return null;
    }
}
