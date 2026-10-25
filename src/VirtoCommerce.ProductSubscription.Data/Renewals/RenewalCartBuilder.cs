using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;
using CartAddress = VirtoCommerce.CartModule.Core.Model.Address;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;
using CustomerOrder = VirtoCommerce.OrdersModule.Core.Model.CustomerOrder;

namespace VirtoCommerce.ProductSubscription.Data.Renewals;

public class RenewalCartBuilder(
    ICartAggregateRepository cartAggregateRepository,
    ICartAvailMethodsService cartAvailMethodsService,
    ICustomerOrderBuilder customerOrderBuilder)
    : IRenewalCartBuilder
{
    public const string RenewalCartType = "SubscriptionRenewal";

    public virtual async Task<RenewalCartResult> BuildAsync(Renewal renewal, IList<Subscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(renewal);

        var subscriptionsById = subscriptions.ToDictionary(x => x.Id);
        var activeItems = renewal.Items.Where(x => !x.IsExcluded && subscriptionsById.ContainsKey(x.SubscriptionId)).ToList();

        var discounts = activeItems
            .Select(x => subscriptionsById[x.SubscriptionId])
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x =>
            {
                var s = x.First();
                return new SubscriptionLineDiscount(s.PlanId, s.PlanName, s.DiscountType, s.DiscountValue);
            });

        using var scope = SubscriptionDiscountScope.Begin(discounts);

        var cart = AbstractTypeFactory<ShoppingCart>.TryCreateInstance();
        cart.StoreId = renewal.StoreId;
        cart.CustomerId = renewal.CustomerId;
        cart.CustomerName = renewal.CustomerName;
        cart.OrganizationId = renewal.OrganizationId;
        cart.Currency = renewal.Currency;
        cart.LanguageCode = renewal.LanguageCode;
        cart.Name = $"Subscription renewal {renewal.Number}";
        cart.Type = RenewalCartType;
        cart.Items = new List<LineItem>();
        cart.Shipments = new List<Shipment>();
        cart.Payments = new List<Payment>();
        cart.Addresses = new List<CartAddress>();

        var aggregate = await cartAggregateRepository.GetCartForShoppingCartAsync(cart, renewal.LanguageCode);

        var result = new RenewalCartResult { Aggregate = aggregate };

        if (activeItems.Count > 0)
        {
            var newItems = activeItems
                .GroupBy(x => x.ProductId)
                .Select(x => new NewCartItem(x.Key, x.Sum(i => subscriptionsById[i.SubscriptionId].Quantity)))
                .ToList();

            await aggregate.AddItemsAsync(newItems);
        }

        var deliveryAddress = ToCartAddress(renewal.ShippingAddress, AddressType.Shipping);
        if (deliveryAddress != null)
        {
            aggregate.Cart.Addresses.Add(deliveryAddress);
        }

        if (aggregate.Cart.Items.Count > 0)
        {
            await AddShipmentAsync(aggregate, renewal, deliveryAddress);
            await AddPaymentAsync(aggregate, renewal, deliveryAddress);
        }

        await aggregate.RecalculateAsync();

        // The payment amount must equal the cart total, as checkout does.
        var payment = aggregate.Cart.Payments.FirstOrDefault();
        if (payment != null)
        {
            payment.Amount = aggregate.Cart.Total;
            await aggregate.RecalculateAsync();
            payment.Amount = aggregate.Cart.Total;
        }

        var validationErrors = (await aggregate.ValidateAsync("items")).Concat(aggregate.OperationValidationErrors).ToList();

        foreach (var item in renewal.Items)
        {
            var subscription = subscriptionsById.GetValueOrDefault(item.SubscriptionId);
            var lineItem = aggregate.Cart.Items.FirstOrDefault(x => x.ProductId == item.ProductId);
            var copy = (RenewalItem)item.Clone();

            if (subscription != null)
            {
                copy.Quantity = subscription.Quantity;
                copy.ProductName = lineItem?.Name ?? subscription.ProductName;
                copy.ImageUrl = lineItem?.ImageUrl ?? subscription.ImageUrl;
                copy.Sku = lineItem?.Sku ?? subscription.Sku;
            }

            if (copy.IsExcluded)
            {
                result.Items.Add(copy);
                continue;
            }

            var errors = validationErrors
                .Where(x => x is CartValidationError cartError && (cartError.ObjectId == lineItem?.Id || cartError.ObjectId == item.ProductId))
                .Select(x => x.ErrorMessage)
                .ToList();

            if (lineItem == null)
            {
                copy.IsAvailable = false;
                copy.Message = errors.FirstOrDefault() ?? "This product is not available right now.";
                copy.ListPrice = 0;
                copy.DiscountAmount = 0;
                copy.Total = 0;
            }
            else
            {
                copy.IsAvailable = errors.Count == 0;
                copy.Message = errors.FirstOrDefault();
                copy.ListPrice = lineItem.ListPrice;
                copy.DiscountAmount = lineItem.DiscountAmount * lineItem.Quantity;
                copy.Total = lineItem.ExtendedPrice;
            }

            if (!copy.IsAvailable)
            {
                result.Problems.Add($"{copy.ProductName}: {copy.Message}");
            }

            result.Items.Add(copy);
        }

        if (aggregate.Cart.Items.Count > 0 && aggregate.Cart.Shipments.Count == 0)
        {
            result.Problems.Add("No shipping method is available for the delivery address.");
        }

        result.Total = aggregate.Cart.Total;
        result.DiscountTotal = aggregate.Cart.DiscountTotal;
        result.ShippingTotal = aggregate.Cart.ShippingTotal;
        result.TaxTotal = aggregate.Cart.TaxTotal;

        return result;
    }

    public virtual async Task<CustomerOrder> PlaceOrderAsync(Renewal renewal, RenewalCartResult cartResult)
    {
        var cart = cartResult.Aggregate.Cart;

        // The converter maps cart lines, shipments and payments by id; give the in-memory cart's objects ids.
        foreach (var lineItem in cart.Items.Where(x => string.IsNullOrEmpty(x.Id)))
        {
            lineItem.Id = Guid.NewGuid().ToString("N");
        }

        foreach (var shipment in cart.Shipments.Where(x => string.IsNullOrEmpty(x.Id)))
        {
            shipment.Id = Guid.NewGuid().ToString("N");
        }

        foreach (var payment in cart.Payments.Where(x => string.IsNullOrEmpty(x.Id)))
        {
            payment.Id = Guid.NewGuid().ToString("N");
        }

        cart.Comment = $"Subscription renewal {renewal.Number}";

        return await customerOrderBuilder.PlaceCustomerOrderFromCartAsync(cart);
    }

    protected virtual async Task AddShipmentAsync(CartAggregate aggregate, Renewal renewal, CartAddress deliveryAddress)
    {
        var rates = (await cartAvailMethodsService.GetAvailableShippingRatesAsync(aggregate))?.ToList() ?? [];
        if (rates.Count == 0)
        {
            return;
        }

        var rate = rates.FirstOrDefault(x => x.ShippingMethod?.Code == renewal.ShippingMethodCode && (string.IsNullOrEmpty(renewal.ShippingMethodOption) || x.OptionName == renewal.ShippingMethodOption))
            ?? rates.FirstOrDefault(x => x.ShippingMethod?.Code == renewal.ShippingMethodCode)
            ?? rates.OrderBy(x => x.Rate).First();

        var shipment = AbstractTypeFactory<Shipment>.TryCreateInstance();
        shipment.ShipmentMethodCode = rate.ShippingMethod?.Code;
        shipment.ShipmentMethodOption = rate.OptionName;
        shipment.Currency = aggregate.Cart.Currency;
        shipment.DeliveryAddress = deliveryAddress;

        await aggregate.AddShipmentAsync(shipment, rates);
    }

    protected virtual async Task AddPaymentAsync(CartAggregate aggregate, Renewal renewal, CartAddress billingAddress)
    {
        var methods = (await cartAvailMethodsService.GetAvailablePaymentMethodsAsync(aggregate))?.ToList() ?? [];
        var method = methods.FirstOrDefault(x => x.Code == renewal.PaymentMethodCode);
        if (method == null)
        {
            return;
        }

        var payment = AbstractTypeFactory<Payment>.TryCreateInstance();
        payment.PaymentGatewayCode = method.Code;
        payment.Currency = aggregate.Cart.Currency;
        payment.BillingAddress = billingAddress == null ? null : ToCartAddress(billingAddress, AddressType.Billing);

        await aggregate.AddPaymentAsync(payment, methods);
    }

    protected static CartAddress ToCartAddress(CoreAddress source, AddressType addressType)
    {
        if (source == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<CartAddress>.TryCreateInstance();
        result.AddressType = addressType;
        result.Name = source.Name;
        result.FirstName = source.FirstName;
        result.LastName = source.LastName;
        result.Organization = source.Organization;
        result.Line1 = source.Line1;
        result.Line2 = source.Line2;
        result.City = source.City;
        result.PostalCode = source.PostalCode;
        result.RegionId = source.RegionId;
        result.RegionName = source.RegionName;
        result.CountryCode = source.CountryCode;
        result.CountryName = source.CountryName;
        result.Phone = source.Phone;
        result.Email = source.Email;
        return result;
    }
}
