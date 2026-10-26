using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
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
        Field(x => x.Status).Description("PendingActivation, Active, Paused, PastDue or Cancelled. A string, not an enum: a solution can add its own statuses.");
        Field(x => x.StoreId);
        Field(x => x.ProductId);
        Field(x => x.Sku, nullable: true);
        Field(x => x.ProductName, nullable: true);
        Field(x => x.ImageUrl, nullable: true);
        Field(x => x.Quantity);
        Field(x => x.PlanId, nullable: true);
        Field(x => x.PlanName, nullable: true);
        Field<NonNullGraphType<SubscriptionIntervalUnitEnum>>("intervalUnit").Resolve(x => x.Source.IntervalUnit);
        Field(x => x.IntervalCount);
        Field<NonNullGraphType<StringGraphType>>("intervalLabel").Resolve(x => SubscriptionSchedule.Describe(x.Source.IntervalUnit, x.Source.IntervalCount));
        Field<NonNullGraphType<SubscriptionDiscountTypeEnum>>("discountType").Resolve(x => x.Source.DiscountType);
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

        // The four fields below load related records through data loaders: one query per page of subscriptions, not one per subscription.
        Field<NonNullGraphType<SubscriptionQuantityRulesType>>("quantityRules")
            .Description("The product's ordering rules the subscription quantity must follow.")
            .Resolve(context =>
            {
                if (string.IsNullOrEmpty(context.Source.ProductId))
                {
                    return ToQuantityRules(null);
                }

                return Load(context, "product_subscription_products", context.Source.ProductId,
                        ids => context.RequestServices.GetRequiredService<IItemService>().GetAsync(ids, ItemResponseGroup.ItemInfo.ToString(), clone: false))
                    .Then(ToQuantityRules);
            });

        Field<VirtoCommerce.XCart.Core.Schemas.SavedPaymentMethodType>("savedPaymentMethod").Resolve(context =>
        {
            if (string.IsNullOrEmpty(context.Source.StoredPaymentMethodId))
            {
                return null;
            }

            return Load(context, "product_subscription_saved_payment_methods", context.Source.StoredPaymentMethodId,
                ids => context.RequestServices.GetRequiredService<IStoredPaymentMethodService>().GetAsync(ids, clone: false));
        });

        Field<RenewalType>("currentRenewal").Resolve(context =>
        {
            if (string.IsNullOrEmpty(context.Source.CurrentRenewalId))
            {
                return null;
            }

            return Load(context, "product_subscription_renewals", context.Source.CurrentRenewalId,
                ids => context.RequestServices.GetRequiredService<IRenewalService>().GetAsync(ids, clone: false));
        });

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionIntervalType>>>>("availableIntervals")
            .Description("Frequencies the customer can switch to")
            .Resolve(context =>
            {
                var subscription = context.Source;
                if (string.IsNullOrEmpty(subscription.PlanId))
                {
                    return new[] { new SubscriptionPlanInterval { Unit = subscription.IntervalUnit, Count = subscription.IntervalCount, IsDefault = true } };
                }

                return Load(context, "product_subscription_plans", subscription.PlanId,
                        ids => context.RequestServices.GetRequiredService<ISubscriptionPlanService>().GetAsync(ids, clone: false))
                    .Then(plan =>
                    {
                        var intervals = plan?.Intervals?.ToList() ?? [];
                        if (!intervals.Any(i => i.Unit == subscription.IntervalUnit && i.Count == subscription.IntervalCount))
                        {
                            intervals.Insert(0, new SubscriptionPlanInterval { Unit = subscription.IntervalUnit, Count = subscription.IntervalCount });
                        }

                        return intervals;
                    });
            });

        Field<NonNullGraphType<BooleanGraphType>>("canSkip").Resolve(x => x.Source.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue);
        Field<NonNullGraphType<BooleanGraphType>>("canPause").Resolve(x => x.Source.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue);
        Field<NonNullGraphType<BooleanGraphType>>("canResume").Resolve(x => x.Source.Status == SubscriptionStatus.Paused);
        Field<NonNullGraphType<BooleanGraphType>>("canCancel").Resolve(x => x.Source.Status != SubscriptionStatus.Cancelled);
        Field<NonNullGraphType<BooleanGraphType>>("canOrderNow").Resolve(x => x.Source.Status == SubscriptionStatus.Active);
        Field<NonNullGraphType<BooleanGraphType>>("canChange").Resolve(x => x.Source.Status != SubscriptionStatus.Cancelled);
    }

    /// <summary>Loads <paramref name="id"/> with the other ids this request asks for under <paramref name="loaderName"/>, in one call.</summary>
    protected static IDataLoaderResult<T> Load<T>(IResolveFieldContext context, string loaderName, string id, Func<IList<string>, Task<IList<T>>> load)
        where T : Entity
    {
        var accessor = context.RequestServices.GetRequiredService<IDataLoaderContextAccessor>();
        var loader = accessor.Context.GetOrAddBatchLoader<string, T>(loaderName, async ids =>
            (IDictionary<string, T>)(await load(ids.ToList())).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First()));

        return loader.LoadAsync(id);
    }

    protected static SubscriptionQuantityRules ToQuantityRules(CatalogProduct product)
    {
        return new SubscriptionQuantityRules(
            product?.PackSize > 1 ? product.PackSize : 1,
            product?.MinQuantity > 0 ? product.MinQuantity.Value : 1,
            product?.MaxQuantity > 0 ? product.MaxQuantity : null);
    }
}
