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
        Field(x => x.ListPrice);
        Field(x => x.DiscountAmount);
        Field(x => x.Total);
        Field(x => x.IsAvailable);
        Field(x => x.IsExcluded);
        Field(x => x.Message, nullable: true);
    }
}
