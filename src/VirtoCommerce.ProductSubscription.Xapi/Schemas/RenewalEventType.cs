using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class RenewalEventType : ExtendableGraphType<RenewalEvent>
{
    public RenewalEventType()
    {
        Name = "SubscriptionRenewalEvent";

        Field(x => x.EventDate);
        Field(x => x.State, nullable: true);
        Field(x => x.EventType, nullable: true);
        Field(x => x.Message, nullable: true);
    }
}
