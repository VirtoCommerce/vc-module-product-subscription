using System;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Import;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Import;

[Trait("Category", "Unit")]
public class LegacySubscriptionJsonMapperTests
{
    private readonly LegacySubscriptionJsonMapper _sut = new();

    // Splitting the order prototype into item-level records

    [Fact]
    public void MapSubscription_SeveralProducts_OneRecordPerProductWithProductQualifiedOuterId()
    {
        // Arrange
        var json = CreateLegacySubscription(items:
        [
            CreateLineItem("product-1", "SKU-1", "Coffee", quantity: 2, placedPrice: 9.5m),
            CreateLineItem("product-2", "SKU-2", "Filters", quantity: 1, placedPrice: 3m),
        ]);

        // Act
        var records = _sut.MapSubscription(json);

        // Assert
        records.Should().HaveCount(2);

        var coffee = records.Single(x => x.ProductId == "product-1");
        coffee.Sku.Should().Be("SKU-1");
        coffee.ProductName.Should().Be("Coffee");
        coffee.Quantity.Should().Be(2);
        coffee.ReferencePrice.Should().Be(9.5m);
        coffee.OuterId.Should().Be("legacy:sub-1:product-1");

        var filters = records.Single(x => x.ProductId == "product-2");
        filters.Quantity.Should().Be(1);
        filters.OuterId.Should().Be("legacy:sub-1:product-2");

        records.Should().AllSatisfy(x =>
        {
            x.LegacySubscriptionId.Should().Be("sub-1");
            x.LegacySubscriptionNumber.Should().Be("SU240101-00001");
            x.StoreId.Should().Be("store-1");
            x.CustomerId.Should().Be("customer-1");
            x.CustomerName.Should().Be("Jane Doe");
            x.OrganizationId.Should().Be("org-1");
            x.Currency.Should().Be("USD");
            x.LanguageCode.Should().Be("en-US");
            x.PaymentMethodCode.Should().Be("AuthorizeNet");
            x.ShippingMethodCode.Should().Be("FixedRate");
            x.ShippingMethodOption.Should().Be("Ground");
        });

        // Each record has its own address instance.
        coffee.ShippingAddress.Should().NotBeSameAs(filters.ShippingAddress);
    }

    [Fact]
    public void MapSubscription_SingleProduct_UsesPlainLegacyOuterId()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);

        // Act
        var record = _sut.MapSubscription(json).Should().ContainSingle().Subject;

        // Assert
        record.ProductId.Should().Be("product-1");
        record.OuterId.Should().Be("legacy:sub-1");
    }

    [Fact]
    public void MapSubscription_GiftCancelledAndRepeatedLines_SkipsGiftsAndCancelledAndMergesRepeats()
    {
        // Arrange
        var gift = CreateLineItem("gift-1");
        gift["isGift"] = true;
        var cancelled = CreateLineItem("product-3");
        cancelled["isCancelled"] = true;

        var json = CreateLegacySubscription(items:
        [
            CreateLineItem("product-1", quantity: 2),
            CreateLineItem("product-1", quantity: 3),
            gift,
            cancelled,
        ]);

        // Act
        var record = _sut.MapSubscription(json).Should().ContainSingle().Subject;

        // Assert
        record.ProductId.Should().Be("product-1");
        record.Quantity.Should().Be(5);
        record.OuterId.Should().Be("legacy:sub-1");
    }

    [Fact]
    public void MapSubscription_NoOrderPrototype_ReturnsOneRecordWithoutProduct()
    {
        // Arrange
        var json = CreateLegacySubscription(items: null);
        json.Remove("customerOrderPrototype");

        // Act
        var record = _sut.MapSubscription(json).Should().ContainSingle().Subject;

        // Assert
        record.ProductId.Should().BeNull();
        record.OuterId.Should().Be("legacy:sub-1");
        record.StoreId.Should().Be("store-1");
    }

    // Interval

    [Theory]
    [InlineData("Days", SubscriptionIntervalUnit.Day)]
    [InlineData("Weeks", SubscriptionIntervalUnit.Week)]
    [InlineData("Months", SubscriptionIntervalUnit.Month)]
    [InlineData("Years", SubscriptionIntervalUnit.Year)]
    [InlineData("months", SubscriptionIntervalUnit.Month)]
    public void MapSubscription_IntervalName_MapsToUnit(string interval, SubscriptionIntervalUnit expected)
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        json["interval"] = interval;
        json["intervalCount"] = 3;

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.IntervalUnit.Should().Be(expected);
        record.IntervalCount.Should().Be(3);
    }

    [Theory]
    [InlineData(0, SubscriptionIntervalUnit.Day)]
    [InlineData(1, SubscriptionIntervalUnit.Week)]
    [InlineData(2, SubscriptionIntervalUnit.Month)]
    [InlineData(3, SubscriptionIntervalUnit.Year)]
    public void ParseInterval_Number_MapsToUnit(int interval, SubscriptionIntervalUnit expected)
    {
        LegacySubscriptionJsonMapper.ParseInterval(new JValue(interval)).Should().Be(expected);
    }

    [Fact]
    public void MapSubscription_MissingOrZeroIntervalCount_UsesOne()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        json["intervalCount"] = 0;

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.IntervalCount.Should().Be(1);
    }

    // Status

    [Theory]
    [InlineData("Active", LegacySubscriptionStatus.Active)]
    [InlineData("Trialing", LegacySubscriptionStatus.Trialing)]
    [InlineData("PastDue", LegacySubscriptionStatus.PastDue)]
    [InlineData("Unpaid", LegacySubscriptionStatus.Unpaid)]
    [InlineData("Cancelled", LegacySubscriptionStatus.Cancelled)]
    public void MapSubscription_StatusName_MapsToLegacyStatus(string status, LegacySubscriptionStatus expected)
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        json["subscriptionStatus"] = status;

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, LegacySubscriptionStatus.Active)]
    [InlineData(1, LegacySubscriptionStatus.Trialing)]
    [InlineData(2, LegacySubscriptionStatus.PastDue)]
    [InlineData(3, LegacySubscriptionStatus.Cancelled)]
    [InlineData(4, LegacySubscriptionStatus.Unpaid)]
    public void ParseStatus_Number_MapsToLegacyStatus(int status, LegacySubscriptionStatus expected)
    {
        LegacySubscriptionJsonMapper.ParseStatus(new JValue(status)).Should().Be(expected);
    }

    [Fact]
    public void MapSubscription_IsCancelled_IsCancelledWithDateAndReason()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        json["isCancelled"] = true;
        json["cancelledDate"] = "2025-03-01T10:00:00Z";
        json["cancelReason"] = "Too much coffee";

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.Status.Should().Be(LegacySubscriptionStatus.Cancelled);
        record.CancelledDate.Should().Be(new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        record.CancelReason.Should().Be("Too much coffee");
    }

    [Fact]
    public void MapSubscription_EndDatePassed_IsCancelledAsExpired()
    {
        // Arrange
        var endDate = DateTime.UtcNow.AddDays(-3);
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        json["endDate"] = endDate;

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.Status.Should().Be(LegacySubscriptionStatus.Cancelled);
        record.CancelledDate.Should().BeCloseTo(endDate, TimeSpan.FromSeconds(1));
        record.CancelReason.Should().Be("Completed with time expiration");
    }

    [Fact]
    public void MapSubscription_ActiveWithBalance_IsUnpaid()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        json["balance"] = 25.5m;

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.Status.Should().Be(LegacySubscriptionStatus.Unpaid);
    }

    // Dates

    [Fact]
    public void MapSubscription_Dates_NextOrderDateIsCurrentPeriodEnd()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.StartDate.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        record.LastOrderDate.Should().Be(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
        record.NextOrderDate.Should().Be(new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc));
        record.NextOrderDate.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void MapSubscription_NoCurrentPeriodEnd_NextOrderDateIsPeriodStartPlusInterval()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        json.Remove("currentPeriodEnd");
        json["interval"] = "Weeks";
        json["intervalCount"] = 2;

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.NextOrderDate.Should().Be(new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc));
    }

    // Address

    [Fact]
    public void MapSubscription_ShipmentDeliveryAddress_IsShippingAddress()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        var address = record.ShippingAddress;
        address.Should().NotBeNull();
        address.AddressType.Should().Be(AddressType.Shipping);
        address.FirstName.Should().Be("Jane");
        address.LastName.Should().Be("Doe");
        address.Line1.Should().Be("1 Main St");
        address.Line2.Should().Be("Apt 2");
        address.City.Should().Be("Springfield");
        address.PostalCode.Should().Be("12345");
        address.RegionId.Should().Be("IL");
        address.RegionName.Should().Be("Illinois");
        address.CountryCode.Should().Be("USA");
        address.CountryName.Should().Be("United States");
        address.Phone.Should().Be("555-0100");
        address.Email.Should().Be("jane@example.com");
        record.CustomerEmail.Should().Be("jane@example.com");
    }

    [Fact]
    public void MapSubscription_NoShipment_UsesShippingAddressOfOrder()
    {
        // Arrange
        var json = CreateLegacySubscription(items: [CreateLineItem("product-1")]);
        var prototype = (JObject)json["customerOrderPrototype"];
        prototype.Remove("shipments");
        prototype["addresses"] = new JArray(
            CreateAddress("Billing", "Billing St", "billing@example.com"),
            CreateAddress("Shipping", "Shipping St", null));

        // Act
        var record = _sut.MapSubscription(json).Single();

        // Assert
        record.ShippingAddress.Line1.Should().Be("Shipping St");
        record.ShippingAddress.AddressType.Should().Be(AddressType.Shipping);
        record.ShippingMethodCode.Should().BeNull();
        record.CustomerEmail.Should().Be("billing@example.com");
    }

    // Reading the request body

    [Fact]
    public void ReadRecords_SearchResult_MapsLegacySubscriptions()
    {
        // Arrange
        var second = CreateLegacySubscription(items: [CreateLineItem("product-9")]);
        second["id"] = "sub-2";
        var json = new JObject
        {
            ["totalCount"] = 2,
            ["results"] = new JArray(CreateLegacySubscription(items: [CreateLineItem("product-1"), CreateLineItem("product-2")]), second),
        };

        // Act
        var isLegacy = _sut.IsLegacyModuleJson(json);
        var records = _sut.ReadRecords(json);

        // Assert
        isLegacy.Should().BeTrue();
        records.Select(x => x.OuterId).Should().BeEquivalentTo("legacy:sub-1:product-1", "legacy:sub-1:product-2", "legacy:sub-2");
    }

    [Fact]
    public void ReadRecords_ArrayOfLegacySubscriptionsInPascalCase_MapsThem()
    {
        // Arrange
        var subscription = new JObject
        {
            ["Id"] = "sub-7",
            ["StoreId"] = "store-1",
            ["CustomerId"] = "customer-1",
            ["Interval"] = "Days",
            ["IntervalCount"] = 10,
            ["SubscriptionStatus"] = "Trialing",
            ["CustomerOrderPrototype"] = new JObject { ["Items"] = new JArray(new JObject { ["ProductId"] = "product-1", ["Quantity"] = 4 }) },
        };

        // Act
        var record = _sut.ReadRecords(new JArray(subscription)).Should().ContainSingle().Subject;

        // Assert
        record.OuterId.Should().Be("legacy:sub-7");
        record.IntervalUnit.Should().Be(SubscriptionIntervalUnit.Day);
        record.IntervalCount.Should().Be(10);
        record.Status.Should().Be(LegacySubscriptionStatus.Trialing);
        record.Quantity.Should().Be(4);
    }

    [Fact]
    public void ReadRecords_ArrayOfImportRecords_ReadsThemAsIs()
    {
        // Arrange
        var json = JArray.Parse("""
            [
              {
                "legacySubscriptionId": "sub-1",
                "storeId": "store-1",
                "customerId": "customer-1",
                "productId": "product-1",
                "quantity": 3,
                "intervalUnit": "Week",
                "intervalCount": 2,
                "status": "PastDue",
                "nextOrderDate": "2026-11-01T00:00:00Z",
                "discountType": "Percent",
                "discountValue": 10,
                "shippingAddress": { "line1": "1 Main St", "countryCode": "USA" }
              }
            ]
            """);

        // Act
        var isLegacy = _sut.IsLegacyModuleJson(json);
        var record = _sut.ReadRecords(json).Should().ContainSingle().Subject;

        // Assert
        isLegacy.Should().BeFalse();
        record.LegacySubscriptionId.Should().Be("sub-1");
        record.ProductId.Should().Be("product-1");
        record.Quantity.Should().Be(3);
        record.IntervalUnit.Should().Be(SubscriptionIntervalUnit.Week);
        record.IntervalCount.Should().Be(2);
        record.Status.Should().Be(LegacySubscriptionStatus.PastDue);
        record.NextOrderDate.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        record.DiscountType.Should().Be(SubscriptionDiscountType.Percent);
        record.DiscountValue.Should().Be(10);
        record.ShippingAddress.Line1.Should().Be("1 Main St");
        record.OuterId.Should().BeNull();
    }

    [Fact]
    public void ReadRecords_PlatformExport_MapsSubscriptions()
    {
        // Arrange
        var json = new JObject
        {
            ["PaymentPlans"] = new JArray(),
            ["Subscriptions"] = new JArray(CreateLegacySubscription(items: [CreateLineItem("product-1")])),
        };

        // Act
        var records = _sut.ReadRecords(json);

        // Assert
        records.Should().ContainSingle().Which.OuterId.Should().Be("legacy:sub-1");
    }

    private static JObject CreateLegacySubscription(JObject[] items)
    {
        var prototype = new JObject
        {
            ["id"] = "prototype-1",
            ["isPrototype"] = true,
            ["storeId"] = "store-1",
            ["customerId"] = "customer-1",
            ["customerName"] = "Jane Doe",
            ["organizationId"] = "org-1",
            ["currency"] = "USD",
            ["languageCode"] = "en-US",
            ["addresses"] = new JArray(CreateAddress("Billing", "Billing St", "billing@example.com")),
            ["shipments"] = new JArray(new JObject
            {
                ["shipmentMethodCode"] = "FixedRate",
                ["shipmentMethodOption"] = "Ground",
                ["deliveryAddress"] = CreateAddress("Shipping", "1 Main St", "jane@example.com"),
            }),
            ["inPayments"] = new JArray(new JObject { ["gatewayCode"] = "AuthorizeNet", ["paymentStatus"] = "Paid" }),
        };

        if (items != null)
        {
            prototype["items"] = new JArray(items.Cast<object>().ToArray());
        }

        return new JObject
        {
            ["id"] = "sub-1",
            ["number"] = "SU240101-00001",
            ["storeId"] = "store-1",
            ["customerId"] = "customer-1",
            ["customerName"] = "Jane Doe",
            ["balance"] = 0,
            ["interval"] = "Months",
            ["intervalCount"] = 1,
            ["trialPeriodDays"] = 0,
            ["subscriptionStatus"] = "Active",
            ["customerOrderPrototypeId"] = "prototype-1",
            ["customerOrderPrototype"] = prototype,
            ["startDate"] = "2024-01-01T00:00:00Z",
            ["currentPeriodStart"] = "2026-09-15T00:00:00Z",
            ["currentPeriodEnd"] = "2026-10-15T00:00:00Z",
            ["isCancelled"] = false,
            ["createdDate"] = "2024-01-01T00:00:00Z",
        };
    }

    private static JObject CreateLineItem(string productId, string sku = "SKU", string name = "Product", int quantity = 1, decimal placedPrice = 10m)
    {
        return new JObject
        {
            ["id"] = Guid.NewGuid().ToString("N"),
            ["productId"] = productId,
            ["sku"] = sku,
            ["name"] = name,
            ["quantity"] = quantity,
            ["price"] = placedPrice + 1,
            ["placedPrice"] = placedPrice,
            ["imageUrl"] = "https://example.com/image.png",
            ["isGift"] = false,
        };
    }

    private static JObject CreateAddress(string addressType, string line1, string email)
    {
        return new JObject
        {
            ["addressType"] = addressType,
            ["firstName"] = "Jane",
            ["lastName"] = "Doe",
            ["line1"] = line1,
            ["line2"] = "Apt 2",
            ["city"] = "Springfield",
            ["postalCode"] = "12345",
            ["regionId"] = "IL",
            ["regionName"] = "Illinois",
            ["countryCode"] = "USA",
            ["countryName"] = "United States",
            ["phone"] = "555-0100",
            ["email"] = email,
        };
    }
}
