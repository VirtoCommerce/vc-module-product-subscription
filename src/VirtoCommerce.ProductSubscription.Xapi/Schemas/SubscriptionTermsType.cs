using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionTermsType : ExtendableGraphType<SubscriptionTerms>
{
    public SubscriptionTermsType()
    {
        Name = "SubscriptionTerms";
        Description = "Auto-renewal terms shown before the payment fields at checkout.";

        Field(x => x.Version);
        Field(x => x.Text);
        Field(x => x.Hash);
    }
}
