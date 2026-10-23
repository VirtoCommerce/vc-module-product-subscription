using System.Linq;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

public class SubscriptionIntervalType : ExtendableGraphType<SubscriptionPlanInterval>
{
    public SubscriptionIntervalType()
    {
        Name = "SubscriptionInterval";
        Description = "A delivery frequency offered for a product, e.g. every 2 weeks.";

        Field<NonNullGraphType<StringGraphType>>("unit").Description("Day, Week, Month or Year").Resolve(x => x.Source.Unit.ToString());
        Field(x => x.Count).Description("Number of units between deliveries");
        Field(x => x.IsDefault).Description("Preselected frequency");
        Field<NonNullGraphType<StringGraphType>>("label").Resolve(x => SubscriptionSchedule.Describe(x.Source.Unit, x.Source.Count));
    }
}

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
        Field<NonNullGraphType<StringGraphType>>("discountType").Description("None, Percent or FixedAmount").Resolve(x => x.Source.DiscountType.ToString());
        Field(x => x.DiscountValue);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionIntervalType>>>>("intervals").Resolve(x => x.Source.Intervals);
    }
}

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

public class SavedPaymentMethodType : ExtendableGraphType<StoredPaymentMethod>
{
    public SavedPaymentMethodType()
    {
        Name = "SavedPaymentMethod";
        Description = "A payment method saved at the payment provider. Never contains the card number or CVV.";

        Field(x => x.Id);
        Field(x => x.PaymentMethodCode);
        Field(x => x.Brand, nullable: true);
        Field(x => x.Last4, nullable: true);
        Field(x => x.ExpirationMonth, nullable: true);
        Field(x => x.ExpirationYear, nullable: true);
        Field(x => x.CardholderName, nullable: true);
        Field(x => x.Status);
        Field(x => x.IsDefault);
        Field<NonNullGraphType<BooleanGraphType>>("isExpired").Resolve(x => x.Source.IsExpired(System.DateTime.UtcNow));
        Field(x => x.CreatedDate);
    }
}

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

public class ProductSubscriptionType : ExtendableGraphType<Subscription>
{
    public ProductSubscriptionType()
    {
        Name = "ProductSubscription";
        Description = "A subscribed product delivered on a schedule.";

        Field(x => x.Id);
        Field(x => x.Number);
        Field(x => x.Status).Description("PendingActivation, Active, Paused, PastDue, Cancelled");
        Field(x => x.StoreId);
        Field(x => x.ProductId);
        Field(x => x.Sku, nullable: true);
        Field(x => x.ProductName, nullable: true);
        Field(x => x.ImageUrl, nullable: true);
        Field(x => x.Quantity);
        Field(x => x.PlanId, nullable: true);
        Field(x => x.PlanName, nullable: true);
        Field<NonNullGraphType<StringGraphType>>("intervalUnit").Resolve(x => x.Source.IntervalUnit.ToString());
        Field(x => x.IntervalCount);
        Field<NonNullGraphType<StringGraphType>>("intervalLabel").Resolve(x => SubscriptionSchedule.Describe(x.Source.IntervalUnit, x.Source.IntervalCount));
        Field<NonNullGraphType<StringGraphType>>("discountType").Resolve(x => x.Source.DiscountType.ToString());
        Field(x => x.DiscountValue);
        Field(x => x.StartDate);
        Field(x => x.NextOrderDate);
        Field(x => x.PausedUntil, nullable: true);
        Field(x => x.SkipCount);
        Field(x => x.CancelledDate, nullable: true);
        Field(x => x.CancelReason, nullable: true);
        Field(x => x.Currency, nullable: true);
        Field(x => x.ShippingMethodCode, nullable: true);
        Field(x => x.ShippingMethodOption, nullable: true);
        Field(x => x.PaymentMethodCode, nullable: true);
        Field(x => x.StoredPaymentMethodId, nullable: true);
        Field(x => x.InitialOrderId, nullable: true);
        Field(x => x.InitialOrderNumber, nullable: true);
        Field(x => x.LastOrderId, nullable: true);
        Field(x => x.LastOrderNumber, nullable: true);
        Field(x => x.LastOrderDate, nullable: true);
        Field(x => x.OrdersCount);
        Field(x => x.ReferencePrice, nullable: true);
        Field(x => x.CreatedDate);
        Field<SubscriptionAddressType>("shippingAddress").Resolve(x => x.Source.ShippingAddress);

        Field<SavedPaymentMethodType>("savedPaymentMethod").ResolveAsync(async context =>
        {
            if (string.IsNullOrEmpty(context.Source.StoredPaymentMethodId))
            {
                return null;
            }

            var service = context.RequestServices.GetRequiredService<IStoredPaymentMethodService>();
            return await service.GetNoCloneAsync(context.Source.StoredPaymentMethodId);
        });

        Field<RenewalType>("currentRenewal").ResolveAsync(async context =>
        {
            if (string.IsNullOrEmpty(context.Source.CurrentRenewalId))
            {
                return null;
            }

            var service = context.RequestServices.GetRequiredService<IRenewalService>();
            return await service.GetNoCloneAsync(context.Source.CurrentRenewalId);
        });

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionIntervalType>>>>("availableIntervals")
            .Description("Frequencies the customer can switch to")
            .ResolveAsync(async context =>
            {
                if (string.IsNullOrEmpty(context.Source.PlanId))
                {
                    return new[] { new SubscriptionPlanInterval { Unit = context.Source.IntervalUnit, Count = context.Source.IntervalCount, IsDefault = true } };
                }

                var service = context.RequestServices.GetRequiredService<ISubscriptionPlanService>();
                var plan = await service.GetNoCloneAsync(context.Source.PlanId);
                var intervals = plan?.Intervals?.ToList() ?? [];
                if (!intervals.Any(i => i.Unit == context.Source.IntervalUnit && i.Count == context.Source.IntervalCount))
                {
                    intervals.Insert(0, new SubscriptionPlanInterval { Unit = context.Source.IntervalUnit, Count = context.Source.IntervalCount });
                }

                return intervals;
            });

        Field<NonNullGraphType<BooleanGraphType>>("canSkip").Resolve(x => x.Source.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue);
        Field<NonNullGraphType<BooleanGraphType>>("canPause").Resolve(x => x.Source.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue);
        Field<NonNullGraphType<BooleanGraphType>>("canResume").Resolve(x => x.Source.Status == SubscriptionStatus.Paused);
        Field<NonNullGraphType<BooleanGraphType>>("canCancel").Resolve(x => x.Source.Status != SubscriptionStatus.Cancelled);
        Field<NonNullGraphType<BooleanGraphType>>("canOrderNow").Resolve(x => x.Source.Status == SubscriptionStatus.Active);
        Field<NonNullGraphType<BooleanGraphType>>("canChange").Resolve(x => x.Source.Status != SubscriptionStatus.Cancelled);
    }
}

public class SubscriptionCartItemType : ExtendableGraphType<SubscriptionCartItem>
{
    public SubscriptionCartItemType()
    {
        Name = "SubscriptionCartItem";
        Description = "The customer's choice to subscribe to a cart line.";

        Field(x => x.Id);
        Field(x => x.CartId);
        Field(x => x.LineItemId);
        Field(x => x.ProductId);
        Field(x => x.PlanId);
        Field<NonNullGraphType<StringGraphType>>("intervalUnit").Resolve(x => x.Source.IntervalUnit.ToString());
        Field(x => x.IntervalCount);
        Field<NonNullGraphType<StringGraphType>>("intervalLabel").Resolve(x => SubscriptionSchedule.Describe(x.Source.IntervalUnit, x.Source.IntervalCount));
    }
}

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
