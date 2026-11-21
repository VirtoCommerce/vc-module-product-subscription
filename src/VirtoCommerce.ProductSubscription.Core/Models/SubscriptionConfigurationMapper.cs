using System;
using System.Collections.Generic;
using System.Linq;
using CartConfigurationItem = VirtoCommerce.CartModule.Core.Model.ConfigurationItem;
using CartLineItem = VirtoCommerce.CartModule.Core.Model.LineItem;
using OrderConfigurationItem = VirtoCommerce.OrdersModule.Core.Model.ConfigurationItem;
using OrderLineItem = VirtoCommerce.OrdersModule.Core.Model.LineItem;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>Builds a <see cref="SubscriptionConfiguration"/> from the configuration of a cart or order line.</summary>
public static class SubscriptionConfigurationMapper
{
    /// <summary>
    /// The configuration of an order line, or null when the line is not a configured product. A configured line with no
    /// option chosen (every section optional) has a configuration without sections, so its renewals are still built
    /// as configured lines.
    /// </summary>
    public static SubscriptionConfiguration FromOrderLine(OrderLineItem lineItem, string orderId = null, string orderNumber = null)
    {
        if (lineItem == null || !lineItem.IsConfigured)
        {
            return null;
        }

        var result = new SubscriptionConfiguration
        {
            ProductId = lineItem.ProductId,
            CapturedFrom = new SubscriptionConfigurationSource { OrderId = orderId, OrderNumber = orderNumber, LineItemId = lineItem.Id },
            CapturedAt = DateTime.UtcNow,
            ConfiguredUnitPrice = lineItem.PlacedPrice > 0 ? lineItem.PlacedPrice : lineItem.Price,
            Sections = (lineItem.ConfigurationItems ?? []).Where(x => x != null).Select(ToSection).ToList(),
        };

        return result;
    }

    /// <summary>
    /// The configuration of a cart line, or null when the line is not a configured product (or was loaded without its
    /// configuration). Unselected options are left out, as checkout does.
    /// </summary>
    public static SubscriptionConfiguration FromCartLine(CartLineItem lineItem)
    {
        if (lineItem == null || !lineItem.IsConfigured || lineItem.ConfigurationItems == null)
        {
            return null;
        }

        var sections = lineItem.ConfigurationItems.Where(x => x is { SelectedForCheckout: true }).Select(ToSection).ToList();

        return new SubscriptionConfiguration
        {
            ProductId = lineItem.ProductId,
            CapturedAt = DateTime.UtcNow,
            ConfiguredUnitPrice = lineItem.ListPrice,
            Sections = sections,
        };
    }

    /// <summary>The fingerprint of a cart line's configuration, or null when the line is not configured.</summary>
    public static string GetHash(CartLineItem lineItem)
    {
        return FromCartLine(lineItem)?.ComputeHash();
    }

    /// <summary>The fingerprint of an order line's configuration, or null when the line is not configured.</summary>
    public static string GetHash(OrderLineItem lineItem)
    {
        return FromOrderLine(lineItem)?.ComputeHash();
    }

    public static SubscriptionConfigurationSection ToSection(OrderConfigurationItem item)
    {
        var files = ToFiles(item.Files?.Select(x => (x.Name, x.Url, x.ContentType, x.Size)));

        return new SubscriptionConfigurationSection
        {
            SectionId = item.SectionId,
            Name = item.SectionName,
            Type = item.Type,
            ProductId = NullIfEmpty(item.ProductId),
            Sku = item.Sku,
            ProductName = item.Name,
            Quantity = item.Quantity,
            UnitPrice = string.IsNullOrEmpty(item.ProductId) ? null : item.SalePrice,
            CustomText = NullIfEmpty(item.CustomText),
            // A section without files has no files list, so the stored JSON leaves it out.
            Files = files.Count > 0 ? files : null,
        };
    }

    public static SubscriptionConfigurationSection ToSection(CartConfigurationItem item)
    {
        var files = ToFiles(item.Files?.Select(x => (x.Name, x.Url, x.ContentType, x.Size)));

        return new SubscriptionConfigurationSection
        {
            SectionId = item.SectionId,
            Name = item.SectionName,
            Type = item.Type,
            ProductId = NullIfEmpty(item.ProductId),
            Sku = item.Sku,
            ProductName = item.Name,
            Quantity = item.Quantity,
            UnitPrice = string.IsNullOrEmpty(item.ProductId) ? null : item.SalePrice,
            CustomText = NullIfEmpty(item.CustomText),
            // A section without files has no files list, so the stored JSON leaves it out.
            Files = files.Count > 0 ? files : null,
        };
    }

    private static List<SubscriptionConfigurationFile> ToFiles(IEnumerable<(string Name, string Url, string ContentType, long Size)> files)
    {
        return (files ?? [])
            .Where(x => !string.IsNullOrEmpty(x.Url))
            .Select(x => new SubscriptionConfigurationFile { Name = x.Name, Url = x.Url, ContentType = x.ContentType, Size = x.Size })
            .ToList();
    }

    private static string NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
