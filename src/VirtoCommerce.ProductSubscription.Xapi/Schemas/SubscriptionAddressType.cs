using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionAddressType : ExtendableGraphType<Address>
{
    public SubscriptionAddressType()
    {
        Name = "SubscriptionAddress";

        Field(x => x.FirstName, nullable: true);
        Field(x => x.LastName, nullable: true);
        Field(x => x.Organization, nullable: true);
        Field(x => x.Line1, nullable: true);
        Field(x => x.Line2, nullable: true);
        Field(x => x.City, nullable: true);
        Field(x => x.PostalCode, nullable: true);
        Field(x => x.RegionId, nullable: true);
        Field(x => x.RegionName, nullable: true);
        Field(x => x.CountryCode, nullable: true);
        Field(x => x.CountryName, nullable: true);
        Field(x => x.Phone, nullable: true);
        Field(x => x.Email, nullable: true);
    }
}
