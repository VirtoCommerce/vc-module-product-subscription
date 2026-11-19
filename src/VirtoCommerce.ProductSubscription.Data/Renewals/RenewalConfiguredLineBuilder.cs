using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Configuration;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Commands;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;
using static VirtoCommerce.CatalogModule.Core.ModuleConstants;
using CartConfigurationSection = VirtoCommerce.XCart.Core.Models.ProductConfigurationSection;
using CatalogConfigurationSection = VirtoCommerce.CatalogModule.Core.Model.Configuration.ProductConfigurationSection;

namespace VirtoCommerce.ProductSubscription.Data.Renewals;

/// <summary>The configured line a renewal ordered, or why the stored configuration can't be ordered as it is.</summary>
public class RenewalConfiguredLineResult
{
    /// <summary>The configured cart line; null when the configuration changed or the product can't be ordered.</summary>
    public LineItem LineItem { get; set; }

    /// <summary>Copy of the stored configuration; sections that can't be ordered have <see cref="SubscriptionConfigurationSection.IsAvailable"/> = false.</summary>
    public SubscriptionConfiguration Configuration { get; set; }

    public bool IsConfigurationChanged { get; set; }

    public string Message { get; set; }
}

public interface IRenewalConfiguredLineBuilder
{
    /// <summary>
    /// Rebuilds a configured product from the configuration stored in the subscription and adds it to the renewal cart,
    /// so xCart prices and validates it like checkout. Never orders a different configuration: when an option was removed
    /// or ran out, nothing is added and the result says the configuration changed.
    /// </summary>
    Task<RenewalConfiguredLineResult> AddAsync(CartAggregate aggregate, Renewal renewal, SubscriptionConfiguration configuration, int quantity);
}

public class RenewalConfiguredLineBuilder(
    IMediator mediator,
    ICartProductService cartProductService,
    IProductConfigurationSearchService configurationSearchService,
    ILogger<RenewalConfiguredLineBuilder> logger)
    : IRenewalConfiguredLineBuilder
{
    public const string ConfigurationChangedMessage = "The configuration of this product can't be ordered as it is any more.";

    public virtual async Task<RenewalConfiguredLineResult> AddAsync(CartAggregate aggregate, Renewal renewal, SubscriptionConfiguration configuration, int quantity)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Sections ??= new List<SubscriptionConfigurationSection>();

        var result = new RenewalConfiguredLineResult { Configuration = (SubscriptionConfiguration)configuration.Clone() };
        foreach (var section in result.Configuration.Sections)
        {
            section.IsAvailable = null;
        }

        var catalogConfiguration = await GetCatalogConfigurationAsync(configuration.ProductId);
        var optionProducts = await GetOptionProductsAsync(aggregate, result.Configuration);

        MarkUnavailableSections(result.Configuration, catalogConfiguration, optionProducts, quantity);

        if (catalogConfiguration == null || result.Configuration.Sections.Any(x => x.IsAvailable == false))
        {
            return Changed(result);
        }

        var configurableProduct = (await cartProductService.GetCartProductsAsync(aggregate, [(aggregate.Currency.Code, configuration.ProductId)])).Values.FirstOrDefault();
        if (configurableProduct == null)
        {
            // The product itself is gone: reported like any unavailable product, not as a configuration change.
            return result;
        }

        LineItem configuredItem;
        try
        {
            configuredItem = await CreateConfiguredItemAsync(aggregate, renewal, configuration, quantity);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation(ex, "Renewal {RenewalNumber}: the configuration of {ProductId} can't be rebuilt", renewal?.Number, configuration.ProductId);
            return Changed(result);
        }

        RestoreFiles(configuredItem, configuration);

        // The rebuilt line must be exactly what the customer subscribed to; anything else is a change to review.
        if (SubscriptionConfigurationMapper.GetHash(configuredItem) != configuration.ComputeHash())
        {
            return Changed(result);
        }

        var newCartItem = new NewCartItem(configuration.ProductId, quantity) { CartProduct = configurableProduct };
        var lineItem = await aggregate.AddConfiguredItemAndReturnAsync(newCartItem, configuredItem);
        if (lineItem == null)
        {
            return Changed(result);
        }

        result.LineItem = lineItem;
        return result;
    }

    protected virtual async Task<ProductConfiguration> GetCatalogConfigurationAsync(string productId)
    {
        var criteria = AbstractTypeFactory<ProductConfigurationSearchCriteria>.TryCreateInstance();
        criteria.ProductId = productId;
        criteria.IsActive = true;
        criteria.Take = 1;

        return (await configurationSearchService.SearchNoCloneAsync(criteria)).Results.FirstOrDefault();
    }

    protected virtual async Task<IDictionary<string, CartProduct>> GetOptionProductsAsync(CartAggregate aggregate, SubscriptionConfiguration configuration)
    {
        var ids = configuration.Sections
            .Where(x => !string.IsNullOrEmpty(x.ProductId))
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, CartProduct>();
        }

        return (await cartProductService.GetCartProductsByIdsAsync(aggregate, ids))
            .Where(x => x != null)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First());
    }

    /// <summary>
    /// Marks each section that can't be ordered as stored: the section or the chosen option was removed from the product,
    /// or the chosen product is no longer sold or doesn't have enough stock for the delivery.
    /// </summary>
    public static void MarkUnavailableSections(
        SubscriptionConfiguration configuration,
        ProductConfiguration catalogConfiguration,
        IDictionary<string, CartProduct> optionProducts,
        int quantity)
    {
        var catalogSections = catalogConfiguration?.Sections?.Where(x => !string.IsNullOrEmpty(x.Id)).ToDictionary(x => x.Id)
            ?? new Dictionary<string, CatalogConfigurationSection>();

        foreach (var section in configuration.Sections)
        {
            section.IsAvailable = catalogSections.TryGetValue(section.SectionId ?? string.Empty, out var catalogSection)
                && IsOptionOffered(section, catalogSection)
                && IsProductAvailable(section, optionProducts, quantity);
        }
    }

    private static bool IsOptionOffered(SubscriptionConfigurationSection section, CatalogConfigurationSection catalogSection)
    {
        if (!string.Equals(section.Type, catalogSection.Type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Product sections list their options; a variation section offers the variations of a product, so it has none.
        var isListedProductSection = string.Equals(section.Type, ConfigurationSectionTypeProduct, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(section.ProductId)
            && catalogSection.Options?.Count > 0;

        return !isListedProductSection || catalogSection.Options.Any(x => x.ProductId == section.ProductId);
    }

    private static bool IsProductAvailable(SubscriptionConfigurationSection section, IDictionary<string, CartProduct> optionProducts, int quantity)
    {
        if (string.IsNullOrEmpty(section.ProductId))
        {
            return true;
        }

        if (!optionProducts.TryGetValue(section.ProductId, out var product))
        {
            return false;
        }

        var requested = (long)Math.Max(1, section.Quantity) * Math.Max(1, quantity);
        return IsSold(product) && HasStock(product, requested);
    }

    /// <summary>
    /// Still sold. No price rule: an option may be free, and xCart builds configured lines from options without one
    /// (xCart's <c>ProductIsAvailableSpecification</c> would reject a free option at every renewal).
    /// </summary>
    private static bool IsSold(CartProduct product)
    {
        return product.Product?.IsActive == true && product.Product.IsBuyable == true;
    }

    private static bool HasStock(CartProduct product, long requested)
    {
        if (product.Product.TrackInventory != true)
        {
            return true;
        }

        var inventory = product.Inventory;
        if (inventory == null)
        {
            return false;
        }

        return inventory.AllowPreorder || inventory.AllowBackorder || product.AvailableQuantity >= requested;
    }

    protected virtual async Task<LineItem> CreateConfiguredItemAsync(CartAggregate aggregate, Renewal renewal, SubscriptionConfiguration configuration, int quantity)
    {
        var command = AbstractTypeFactory<CreateConfiguredLineItemCommand>.TryCreateInstance();
        command.StoreId = aggregate.Cart.StoreId;
        command.UserId = aggregate.Cart.CustomerId;
        command.OrganizationId = aggregate.Cart.OrganizationId;
        command.CultureName = aggregate.Cart.LanguageCode;
        command.CurrencyCode = aggregate.Currency.Code;
        command.ConfigurableProductId = configuration.ProductId;
        command.Quantity = quantity;
        command.ConfigurationSections = configuration.Sections.Select(ToCartSection).ToList();

        var response = await mediator.Send(command);
        return response?.Item ?? throw new InvalidOperationException($"The configured product {configuration.ProductId} was not created for renewal {renewal?.Number}.");
    }

    public static CartConfigurationSection ToCartSection(SubscriptionConfigurationSection section)
    {
        var result = new CartConfigurationSection
        {
            SectionId = section.SectionId,
            SectionName = section.Name,
            Type = section.Type,
            CustomText = section.CustomText,
            FileUrls = section.Files?.Select(x => x.Url).ToList() ?? [],
        };

        if (!string.IsNullOrEmpty(section.ProductId))
        {
            result.Option = new ConfigurableProductOption { ProductId = section.ProductId, Quantity = Math.Max(1, section.Quantity) };
        }

        return result;
    }

    /// <summary>
    /// xCart attaches a file to a configuration only while it belongs to the cart it was uploaded to. The renewal cart is
    /// new, so the stored files of the first order are attached here.
    /// </summary>
    public static void RestoreFiles(LineItem configuredItem, SubscriptionConfiguration configuration)
    {
        if (configuredItem?.ConfigurationItems == null)
        {
            return;
        }

        var storedFiles = configuration.Sections
            .Where(x => x.Files?.Count > 0 && !string.IsNullOrEmpty(x.SectionId))
            .GroupBy(x => x.SectionId)
            .ToDictionary(x => x.Key, x => x.First().Files);

        foreach (var item in configuredItem.ConfigurationItems.Where(x => x.Files.IsNullOrEmpty()))
        {
            if (storedFiles.TryGetValue(item.SectionId ?? string.Empty, out var files))
            {
                item.Files = files.Select(x => new ConfigurationItemFile { Name = x.Name, Url = x.Url, ContentType = x.ContentType, Size = x.Size }).ToList();
            }
        }
    }

    private static RenewalConfiguredLineResult Changed(RenewalConfiguredLineResult result)
    {
        result.IsConfigurationChanged = true;

        var names = result.Configuration.Sections
            .Where(x => x.IsAvailable == false)
            .Select(x => x.ProductName ?? x.Name ?? x.SectionId)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        result.Message = names.Count > 0
            ? $"{ConfigurationChangedMessage} No longer available: {string.Join(", ", names)}."
            : ConfigurationChangedMessage;

        return result;
    }
}
