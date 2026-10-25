using System.Linq;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.ProductSubscription.Xapi.Schemas;

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

        Field<NonNullGraphType<SubscriptionQuantityRulesType>>("quantityRules")
            .Description("The product's ordering rules the subscription quantity must follow.")
            .ResolveAsync(async context =>
            {
                var itemService = context.RequestServices.GetRequiredService<IItemService>();
                var product = string.IsNullOrEmpty(context.Source.ProductId)
                    ? null
                    : await itemService.GetNoCloneAsync(context.Source.ProductId, ItemResponseGroup.ItemInfo.ToString());

                return new SubscriptionQuantityRules(
                    product?.PackSize > 1 ? product.PackSize : 1,
                    product?.MinQuantity > 0 ? product.MinQuantity.Value : 1,
                    product?.MaxQuantity > 0 ? product.MaxQuantity : null);
            });

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
