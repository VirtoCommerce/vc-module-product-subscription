using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// What the customer built for a configurable product in the first order, copied into the subscription so every
/// delivery is rebuilt from it. It is a snapshot, not a link: carts are deleted, and a renewal months later must still
/// know the options. Stored as JSON; <see cref="SchemaVersion"/> lets later versions read older documents.
/// </summary>
public class SubscriptionConfiguration : ICloneable
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>The configurable product.</summary>
    public string ProductId { get; set; }

    public SubscriptionConfigurationSource CapturedFrom { get; set; }

    public DateTime? CapturedAt { get; set; }

    public IList<SubscriptionConfigurationSection> Sections { get; set; } = new List<SubscriptionConfigurationSection>();

    /// <summary>Price of one configured product when the configuration was captured. For display and price checks only.</summary>
    public decimal? ConfiguredUnitPrice { get; set; }

    /// <summary>
    /// The fingerprint of the configuration: SHA-256 of the sections without names and prices, so the same choices give
    /// the same fingerprint whatever the catalog calls them today. Lowercase hex, 64 characters; null when there are no sections.
    /// </summary>
    public virtual string ComputeHash()
    {
        return ComputeHash(Sections);
    }

    public static string ComputeHash(IEnumerable<SubscriptionConfigurationSection> sections)
    {
        var lines = (sections ?? [])
            .Where(x => x != null)
            .Select(GetCanonicalLine)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (lines.Count == 0)
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetCanonicalLine(SubscriptionConfigurationSection section)
    {
        var fileUrls = (section.Files ?? [])
            .Select(x => x?.Url ?? string.Empty)
            .OrderBy(x => x, StringComparer.Ordinal);

        // Quantity counts only for a chosen product: text and file sections carry whatever quantity the cart gave them.
        var quantity = string.IsNullOrEmpty(section.ProductId) ? string.Empty : section.Quantity.ToString(CultureInfo.InvariantCulture);

        // Each value is escaped so a separator inside a custom text can't make two configurations look the same.
        return string.Join('|',
            Escape(section.SectionId),
            Escape(section.Type?.ToUpperInvariant()),
            Escape(section.ProductId),
            quantity,
            Escape(section.CustomText),
            string.Join(',', fileUrls.Select(Escape)));
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value ?? string.Empty);
    }

    public virtual object Clone()
    {
        var result = (SubscriptionConfiguration)MemberwiseClone();
        result.CapturedFrom = (SubscriptionConfigurationSource)CapturedFrom?.Clone();
        result.Sections = Sections?.Select(x => (SubscriptionConfigurationSection)x.Clone()).ToList();
        return result;
    }
}

/// <summary>The order line the configuration was copied from.</summary>
public class SubscriptionConfigurationSource : ICloneable
{
    public string OrderId { get; set; }
    public string OrderNumber { get; set; }
    public string LineItemId { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}

/// <summary>One choice of the configuration: a product, a text or files for one section of the configurable product.</summary>
public class SubscriptionConfigurationSection : ICloneable
{
    public string SectionId { get; set; }

    /// <summary>Section name when captured, for display.</summary>
    public string Name { get; set; }

    /// <summary>Product, Variation, Text or File, as the catalog names the section types.</summary>
    public string Type { get; set; }

    public string ProductId { get; set; }
    public string Sku { get; set; }

    /// <summary>Name of the chosen product when captured, for display.</summary>
    public string ProductName { get; set; }

    public int Quantity { get; set; }

    /// <summary>Unit price of the chosen product when captured, for display.</summary>
    public decimal? UnitPrice { get; set; }

    public string CustomText { get; set; }

    public IList<SubscriptionConfigurationFile> Files { get; set; }

    /// <summary>
    /// Set on the copy in a renewal item only: false when this choice can't be ordered any more (the option was removed
    /// from the product or is out of stock). Null in the subscription itself.
    /// </summary>
    public bool? IsAvailable { get; set; }

    public virtual object Clone()
    {
        var result = (SubscriptionConfigurationSection)MemberwiseClone();
        result.Files = Files?.Select(x => (SubscriptionConfigurationFile)x.Clone()).ToList();
        return result;
    }
}

public class SubscriptionConfigurationFile : ICloneable
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
