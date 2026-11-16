using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using GraphQL.Execution;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Pricing;
using VirtoCommerce.ProductSubscription.Xapi.Queries;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Xapi;

/// <summary>Money fields are formatted by the server in the record's currency, and offer prices match the cart.</summary>
[Trait("Category", "Unit")]
public class SubscriptionMoneyTests
{
    private static readonly Currency _usd = new(new Language("en-US"), "USD", "US dollar", "$", 1m) { RoundingPolicy = new DefaultMoneyRoundingPolicy() };
    private static readonly Currency _eur = new(new Language("de-DE"), "EUR", "Euro", "€", 1m) { RoundingPolicy = new DefaultMoneyRoundingPolicy() };

    private static ResolveFieldContext CreateContext(string cultureName = null)
    {
        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(x => x.GetAllCurrenciesAsync()).ReturnsAsync([_usd, _eur]);

        var services = new ServiceCollection().AddSingleton(currencyService.Object).BuildServiceProvider();
        var context = new ResolveFieldContext
        {
            RequestServices = services,
            UserContext = new Dictionary<string, object>(),
            Arguments = cultureName == null
                ? new Dictionary<string, ArgumentValue>()
                : new Dictionary<string, ArgumentValue> { [SubscriptionMoney.CultureNameArgument] = new(cultureName, ArgumentSource.Literal) },
        };

        return context;
    }

    [Fact]
    public async Task ToMoneyAsync_UsesTheRecordsCurrency_NotUsd()
    {
        var context = CreateContext("de-DE");
        SubscriptionMoney.RememberCultureName(context);

        var money = await SubscriptionMoney.ToMoneyAsync(context, 12.5m, "EUR");

        money.Currency.Code.Should().Be("EUR");
        money.Amount.Should().Be(12.5m);
        money.FormattedAmount.Should().Contain("12,50").And.Contain("€");
    }

    [Fact]
    public async Task ToMoneyAsync_FallsBackToTheRecordsLanguage()
    {
        var money = await SubscriptionMoney.ToMoneyAsync(CreateContext(), 12.5m, "USD", "en-US");

        money.FormattedAmount.Should().Be("$12.50");
    }

    [Theory]
    [InlineData(null, "USD")]
    [InlineData(1, null)]
    [InlineData(1, "XYZ")]
    public async Task ToMoneyAsync_ReturnsNull_WithoutAnAmountOrAKnownCurrency(int? amount, string currencyCode)
    {
        var money = await SubscriptionMoney.ToMoneyAsync(CreateContext(), amount, currencyCode);

        money.Should().BeNull();
    }

    [Fact]
    public async Task Offers_ArePricedLikeTheCart_InTheRequestedCurrency()
    {
        var offerService = new Mock<ISubscriptionOfferService>();
        offerService
            .Setup(x => x.GetOffersAsync("store-1", It.IsAny<IList<string>>()))
            .ReturnsAsync(new Dictionary<string, SubscriptionOffer>
            {
                ["P1"] = new() { ProductId = "P1", PlanId = "plan-1", DiscountType = SubscriptionDiscountType.Percent, DiscountValue = 10m },
                ["P2"] = new() { ProductId = "P2", PlanId = "plan-2", DiscountType = SubscriptionDiscountType.FixedAmount, DiscountValue = 50m },
                ["P3"] = new() { ProductId = "P3", PlanId = "plan-1", DiscountType = SubscriptionDiscountType.Percent, DiscountValue = 10m },
            });

        LoadProductsQuery sentQuery = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<LoadProductsQuery>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<LoadProductResponse> query, CancellationToken _) => sentQuery = (LoadProductsQuery)query)
            .ReturnsAsync(new LoadProductResponse([CreateProduct("P1", list: 30m, sale: 25m), CreateProduct("P2", list: 40m, sale: 40m)]));

        var sut = new GetSubscriptionOffersQueryHandler(offerService.Object, new SubscriptionDiscountCalculator(), mediator.Object);

        var offers = await sut.Handle(new GetSubscriptionOffersQuery
        {
            StoreId = "store-1",
            ProductIds = ["P1", "P2", "P3"],
            CurrencyCode = "EUR",
            CultureName = "de-DE",
            UserId = "user-1",
        }, CancellationToken.None);

        sentQuery.CurrencyCode.Should().Be("EUR");
        sentQuery.UserId.Should().Be("user-1");
        sentQuery.IncludeFields.Should().Contain(x => x.Contains("price"));

        var percent = offers.Single(x => x.ProductId == "P1");
        percent.Currency.Should().Be("EUR");
        percent.Price.Should().Be(25m);
        percent.DiscountAmount.Should().Be(2.5m);
        percent.SubscriptionPrice.Should().Be(22.5m);

        var fixedAmount = offers.Single(x => x.ProductId == "P2");
        fixedAmount.DiscountAmount.Should().Be(40m, "the discount never exceeds the price");
        fixedAmount.SubscriptionPrice.Should().Be(0m);

        var withoutPrice = offers.Single(x => x.ProductId == "P3");
        withoutPrice.Price.Should().BeNull();
        withoutPrice.SubscriptionPrice.Should().BeNull();
        withoutPrice.Currency.Should().BeNull();
    }

    [Fact]
    public void Schema_ExposesMoneyAndCodeFields()
    {
        // The schema camel-cases field names, so compare them that way.
        FieldNames(new RenewalType()).Should().Contain(["previewTotalMoney", "notifiedTotalMoney", "orderTotalMoney", "lastErrorCode", "reviewReasonCode"]);
        FieldNames(new RenewalItemType()).Should().Contain("totalMoney");
        FieldNames(new ProductSubscriptionType()).Should().Contain(["referencePriceMoney", "discountValueMoney"]);
        FieldNames(new SubscriptionOfferType()).Should().Contain(["currency", "price", "discountAmount", "subscriptionPrice"]);
    }

    private static IEnumerable<string> FieldNames(GraphQL.Types.IComplexGraphType type)
    {
        return type.Fields.Select(x => char.ToLowerInvariant(x.Name[0]) + x.Name[1..]);
    }

    private static ExpProduct CreateProduct(string id, decimal list, decimal sale)
    {
        var price = new ProductPrice(_eur)
        {
            ProductId = id,
            ListPrice = new Money(list, _eur),
            SalePrice = new Money(sale, _eur),
            DiscountAmount = new Money(list - sale, _eur),
        };

        return new ExpProduct
        {
            IndexedProduct = new CatalogProduct { Id = id },
            AllPrices = [price],
        };
    }
}
