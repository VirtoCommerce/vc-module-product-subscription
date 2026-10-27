using System.Linq;
using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class RenewalType : ExtendableGraphType<Renewal>
{
    public RenewalType()
    {
        Name = "SubscriptionRenewal";
        Description = "One upcoming or past delivery of one or more subscriptions.";

        Field(x => x.Id);
        Field(x => x.Number);
        Field(x => x.State).Description("Scheduled, Notified, ReviewRequired, Ordered, PaymentActionRequired, RetryScheduled, Authorized, Completed, Failed, Skipped, Cancelled");
        Field(x => x.ScheduledDate);
        Field(x => x.CutoffDate);
        Field(x => x.IsManual);
        Field(x => x.StoredPaymentMethodId, nullable: true).Description("The saved card this delivery is charged to");
        Field(x => x.Currency, nullable: true);
        Field(x => x.PreviewTotal);
        Field(x => x.NotifiedTotal, nullable: true);
        Field(x => x.OrderTotal, nullable: true);
        Field(x => x.DiscountTotal);
        Field(x => x.ShippingTotal);
        Field(x => x.TaxTotal);
        Field(x => x.OrderId, nullable: true);
        Field(x => x.OrderNumber, nullable: true);
        Field(x => x.AttemptCount);
        Field(x => x.NextAttemptDate, nullable: true);
        Field(x => x.LastErrorMessage, nullable: true);
        Field(x => x.ReviewReason, nullable: true);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<RenewalItemType>>>>("items").Resolve(x => x.Source.Items);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<RenewalEventType>>>>("events")
            .Description("Customer-visible history")
            .Resolve(x => x.Source.Events.Where(e => e.EventType != Core.Models.RenewalEventType.Error).OrderByDescending(e => e.EventDate).ToList());
        Field<NonNullGraphType<BooleanGraphType>>("canSkip").Resolve(x => RenewalState.BeforeOrder.Contains(x.Source.State));
        Field<NonNullGraphType<BooleanGraphType>>("canAcceptReview").Resolve(x => x.Source.State == RenewalState.ReviewRequired);
        Field<NonNullGraphType<BooleanGraphType>>("canRetryPayment").Resolve(x => x.Source.State is RenewalState.Failed or RenewalState.RetryScheduled or RenewalState.PaymentActionRequired && !string.IsNullOrEmpty(x.Source.OrderId));
        Field<NonNullGraphType<BooleanGraphType>>("requiresPaymentAction").Resolve(x => x.Source.State == RenewalState.PaymentActionRequired);
    }
}
