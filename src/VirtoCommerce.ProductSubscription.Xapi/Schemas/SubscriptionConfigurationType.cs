using GraphQL;
using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionConfigurationType : ExtendableGraphType<SubscriptionConfiguration>
{
    public SubscriptionConfigurationType()
    {
        Name = "SubscriptionConfiguration";
        Description = "The options of a configurable product, copied from the first order. Every delivery orders this configuration.";

        Field(x => x.SchemaVersion);
        Field(x => x.ProductId, nullable: true);
        Field<SubscriptionConfigurationSourceType>("capturedFrom").Resolve(x => x.Source.CapturedFrom);
        Field(x => x.CapturedAt, nullable: true);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionConfigurationSectionType>>>>("sections").Resolve(x => x.Source.Sections ?? []);
        Field(x => x.ConfiguredUnitPrice, nullable: true).Description("Price of one configured product when the configuration was captured");
        Field<MoneyType>("configuredUnitPriceMoney")
            .Description("ConfiguredUnitPrice as money, in the currency of the subscription or renewal")
            .ResolveAsync(async context =>
            {
                var (currency, language) = GetCurrency(context);
                return await SubscriptionMoney.ToMoneyAsync(context, context.Source.ConfiguredUnitPrice, currency, language);
            });
    }

    /// <summary>Currency and language of the nearest subscription or renewal above the field.</summary>
    public static (string Currency, string Language) GetCurrency(IResolveFieldContext context)
    {
        for (var parent = context.Parent; parent != null; parent = parent.Parent)
        {
            switch (parent.Source)
            {
                case Subscription subscription:
                    return (subscription.Currency, subscription.LanguageCode);
                case Renewal renewal:
                    return (renewal.Currency, renewal.LanguageCode);
            }
        }

        return (null, null);
    }
}

public class SubscriptionConfigurationSourceType : ExtendableGraphType<SubscriptionConfigurationSource>
{
    public SubscriptionConfigurationSourceType()
    {
        Name = "SubscriptionConfigurationSource";
        Description = "The order line the configuration was copied from.";

        Field(x => x.OrderId, nullable: true);
        Field(x => x.OrderNumber, nullable: true);
        Field(x => x.LineItemId, nullable: true);
    }
}

public class SubscriptionConfigurationSectionType : ExtendableGraphType<SubscriptionConfigurationSection>
{
    public SubscriptionConfigurationSectionType()
    {
        Name = "SubscriptionConfigurationSection";

        Field(x => x.SectionId, nullable: true);
        Field(x => x.Name, nullable: true).Description("Section name when captured");
        Field(x => x.Type, nullable: true).Description("Product, Variation, Text or File");
        Field(x => x.ProductId, nullable: true);
        Field(x => x.Sku, nullable: true);
        Field(x => x.ProductName, nullable: true).Description("Name of the chosen product when captured");
        Field(x => x.Quantity);
        Field(x => x.UnitPrice, nullable: true);
        Field(x => x.CustomText, nullable: true);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionConfigurationFileType>>>>("files").Resolve(x => x.Source.Files ?? []);
        Field(x => x.IsAvailable, nullable: true)
            .Description("On a renewal item only: false when this choice can't be ordered any more. Null on the subscription.");
    }
}

public class SubscriptionConfigurationFileType : ExtendableGraphType<SubscriptionConfigurationFile>
{
    public SubscriptionConfigurationFileType()
    {
        Name = "SubscriptionConfigurationFile";

        Field(x => x.Name, nullable: true);
        Field(x => x.Url, nullable: true);
        Field(x => x.ContentType, nullable: true);
        Field(x => x.Size);
    }
}
