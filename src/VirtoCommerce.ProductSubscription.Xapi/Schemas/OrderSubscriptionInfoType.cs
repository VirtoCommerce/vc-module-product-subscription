using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class OrderSubscriptionInfoType : ExtendableGraphType<OrderSubscriptionInfo>
{
    public OrderSubscriptionInfoType()
    {
        Name = "OrderSubscriptionInfo";
        Description = "How an order relates to subscriptions: the renewal that placed it and the subscriptions behind its lines.";

        Field(x => x.OrderId);
        Field(x => x.RenewalId, nullable: true).Description("The renewal that placed this order on a delivery date; null for an order the customer placed");
        Field(x => x.RenewalNumber, nullable: true);
        Field(x => x.RenewalScheduledDate, nullable: true).Description("The delivery date of that renewal");
        Field(x => x.IsManualRenewal).Description("The renewal was ordered on request (Order now), not on its date");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<OrderSubscriptionLineType>>>>("subscriptions")
            .Description("The subscriptions this order started or delivered, one per product")
            .Resolve(x => x.Source.Subscriptions);
    }
}

public class OrderSubscriptionLineType : ExtendableGraphType<OrderSubscriptionLine>
{
    public OrderSubscriptionLineType()
    {
        Name = "OrderSubscriptionLine";
        Description = "The subscription behind one product of an order.";

        Field(x => x.SubscriptionId);
        Field(x => x.SubscriptionNumber);
        Field(x => x.ProductId);
        Field<NonNullGraphType<SubscriptionIntervalUnitEnum>>("intervalUnit").Resolve(x => x.Source.IntervalUnit);
        Field(x => x.IntervalCount);
        Field<NonNullGraphType<StringGraphType>>("intervalLabel").Resolve(x => SubscriptionSchedule.Describe(x.Source.IntervalUnit, x.Source.IntervalCount));
        Field(x => x.Status);
        Field(x => x.IsStartedByOrder).Description("This order is the subscription's first one");
    }
}
