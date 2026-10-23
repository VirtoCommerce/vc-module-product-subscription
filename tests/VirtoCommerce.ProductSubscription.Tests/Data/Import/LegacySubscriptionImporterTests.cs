using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Import;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Import;

[Trait("Category", "Unit")]
public class LegacySubscriptionImporterTests
{
    private const string StoreId = "store-1";
    private const string ProductId = "product-1";

    private readonly Mock<ISubscriptionService> _subscriptionServiceMock = new();
    private readonly Mock<ISubscriptionSearchService> _subscriptionSearchServiceMock = new();
    private readonly Mock<ISubscriptionOfferService> _offerServiceMock = new();
    private readonly Mock<IStoreService> _storeServiceMock = new();
    private readonly Mock<IItemService> _itemServiceMock = new();

    private readonly List<Subscription> _existing = [];
    private readonly List<Subscription> _saved = [];
    private readonly List<SubscriptionSearchCriteria> _searches = [];
    private readonly Dictionary<string, SubscriptionOffer> _offers = [];
    private readonly List<CatalogProduct> _products = [new() { Id = ProductId, Code = "SKU-1", Name = "Coffee", Images = [new Image { Url = "https://example.com/coffee.png" }] }];

    public LegacySubscriptionImporterTests()
    {
        _subscriptionSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((SubscriptionSearchCriteria criteria, bool _) =>
            {
                _searches.Add(criteria);
                var results = _existing.Where(x => criteria.OuterIds?.Contains(x.OuterId) == true).ToList();
                return new SubscriptionSearchResult { Results = results, TotalCount = results.Count };
            });

        _subscriptionServiceMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<Subscription>>()))
            .Callback((IList<Subscription> models) =>
            {
                foreach (var model in models)
                {
                    model.Id ??= Guid.NewGuid().ToString("N");
                    model.Number ??= $"SUB-{_saved.Count + 1}";
                    _saved.Add(model);
                }
            })
            .Returns(Task.CompletedTask);

        _storeServiceMock
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => ids
                .Where(x => x == StoreId)
                .Select(x => new Store { Id = x, DefaultCurrency = "USD", DefaultLanguage = "en-US" })
                .ToList());

        _itemServiceMock
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) => _products.Where(x => ids.Contains(x.Id)).ToList());

        _offerServiceMock
            .Setup(x => x.GetOffersAsync(It.IsAny<string>(), It.IsAny<IList<string>>()))
            .ReturnsAsync((string _, IList<string> productIds) => _offers
                .Where(x => productIds.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value));
    }

    private LegacySubscriptionImporter CreateSut() => new(
        _subscriptionServiceMock.Object,
        _subscriptionSearchServiceMock.Object,
        _offerServiceMock.Object,
        _storeServiceMock.Object,
        _itemServiceMock.Object,
        NullLogger<LegacySubscriptionImporter>.Instance);

    // Creating subscriptions

    [Fact]
    public async Task ImportAsync_ValidActiveRecord_CreatesActiveSubscriptionWithoutSavedCard()
    {
        // Arrange
        var nextOrderDate = DateTime.UtcNow.AddDays(10);
        var record = CreateRecord(nextOrderDate: nextOrderDate);

        // Act
        var result = await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        result.DryRun.Should().BeFalse();
        result.TotalCount.Should().Be(1);
        result.CreatedCount.Should().Be(1);
        result.SkippedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);

        var subscription = _saved.Should().ContainSingle().Subject;
        subscription.OuterId.Should().Be("legacy:sub-1");
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.StoreId.Should().Be(StoreId);
        subscription.CustomerId.Should().Be("customer-1");
        subscription.ProductId.Should().Be(ProductId);
        subscription.Quantity.Should().Be(2);
        subscription.IntervalUnit.Should().Be(SubscriptionIntervalUnit.Month);
        subscription.IntervalCount.Should().Be(1);
        subscription.NextOrderDate.Should().Be(nextOrderDate);
        subscription.StoredPaymentMethodId.Should().BeNull();
        subscription.ConsentId.Should().BeNull();
        subscription.PaymentMethodCode.Should().Be("AuthorizeNet");
        subscription.ShippingAddress.Line1.Should().Be("1 Main St");
        subscription.ShippingAddress.Should().NotBeSameAs(record.ShippingAddress);
        subscription.Number.Should().NotBe("SU-OLD-1", "imported subscriptions get numbers from this module");

        // Filled from the catalog and the store when the record has no value.
        subscription.Sku.Should().Be("SKU-1");
        subscription.ProductName.Should().Be("Coffee");
        subscription.ImageUrl.Should().Be("https://example.com/coffee.png");
        subscription.Currency.Should().Be("USD");
        subscription.LanguageCode.Should().Be("en-US");

        var recordResult = result.Records.Should().ContainSingle().Subject;
        recordResult.Outcome.Should().Be(SubscriptionImportOutcome.Created);
        recordResult.SubscriptionId.Should().Be(subscription.Id);
        recordResult.SubscriptionNumber.Should().Be(subscription.Number);
        recordResult.LegacySubscriptionNumber.Should().Be("SU-OLD-1");
        recordResult.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Theory]
    [InlineData(LegacySubscriptionStatus.Active, SubscriptionStatus.Active)]
    [InlineData(LegacySubscriptionStatus.Trialing, SubscriptionStatus.Active)]
    [InlineData(LegacySubscriptionStatus.PastDue, SubscriptionStatus.PastDue)]
    [InlineData(LegacySubscriptionStatus.Unpaid, SubscriptionStatus.PastDue)]
    [InlineData(LegacySubscriptionStatus.Cancelled, SubscriptionStatus.Cancelled)]
    public async Task ImportAsync_LegacyStatus_MapsToStatus(LegacySubscriptionStatus legacyStatus, string expected)
    {
        // Arrange
        var record = CreateRecord();
        record.Status = legacyStatus;

        // Act
        await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        _saved.Should().ContainSingle().Which.Status.Should().Be(expected);
    }

    [Fact]
    public async Task ImportAsync_Cancelled_KeepsCancelDateAndReason()
    {
        // Arrange
        var cancelledDate = new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var record = CreateRecord();
        record.Status = LegacySubscriptionStatus.Cancelled;
        record.CancelledDate = cancelledDate;
        record.CancelReason = "  Moved away ";

        // Act
        await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        var subscription = _saved.Should().ContainSingle().Subject;
        subscription.CancelledDate.Should().Be(cancelledDate);
        subscription.CancelReason.Should().Be("Moved away");
    }

    // Idempotency

    [Fact]
    public async Task ImportAsync_AlreadyImported_SkipsAsDuplicateWithoutSaving()
    {
        // Arrange
        _existing.Add(new Subscription { Id = "existing-1", Number = "SUB-9", OuterId = "legacy:sub-1", Status = SubscriptionStatus.Active });

        // Act
        var result = await CreateSut().ImportAsync([CreateRecord()], dryRun: false);

        // Assert
        result.SkippedCount.Should().Be(1);
        result.CreatedCount.Should().Be(0);
        var recordResult = result.Records.Should().ContainSingle().Subject;
        recordResult.Outcome.Should().Be(SubscriptionImportOutcome.SkippedDuplicate);
        recordResult.SubscriptionId.Should().Be("existing-1");
        recordResult.SubscriptionNumber.Should().Be("SUB-9");

        _searches.Should().ContainSingle().Which.OuterIds.Should().BeEquivalentTo("legacy:sub-1");
        _subscriptionServiceMock.Verify(x => x.SaveChangesAsync(It.IsAny<IList<Subscription>>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_RunTwice_SecondRunSkipsEverything()
    {
        // Arrange
        var sut = CreateSut();
        await sut.ImportAsync([CreateRecord()], dryRun: false);
        _existing.AddRange(_saved);

        // Act
        var result = await sut.ImportAsync([CreateRecord()], dryRun: false);

        // Assert
        result.SkippedCount.Should().Be(1);
        _saved.Should().ContainSingle();
    }

    [Fact]
    public async Task ImportAsync_ExplicitOuterId_IsUsedAsIdempotencyKey()
    {
        // Arrange
        var record = CreateRecord();
        record.OuterId = "legacy:sub-1:product-1";

        // Act
        var result = await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        result.Records.Single().OuterId.Should().Be("legacy:sub-1:product-1");
        _saved.Single().OuterId.Should().Be("legacy:sub-1:product-1");
    }

    [Fact]
    public async Task ImportAsync_SameOuterIdTwiceInOneImport_FailsTheSecond()
    {
        // Act
        var result = await CreateSut().ImportAsync([CreateRecord(), CreateRecord()], dryRun: false);

        // Assert
        result.CreatedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Records[1].Outcome.Should().Be(SubscriptionImportOutcome.Failed);
        result.Records[1].Messages.Should().ContainSingle().Which.Should().Contain("same OuterId");
        _saved.Should().ContainSingle();
    }

    // Dry run

    [Fact]
    public async Task ImportAsync_DryRun_ReportsWithoutSaving()
    {
        // Arrange
        var invalid = CreateRecord();
        invalid.LegacySubscriptionId = "sub-2";
        invalid.CustomerId = null;

        // Act
        var result = await CreateSut().ImportAsync([CreateRecord(), invalid], dryRun: true);

        // Assert
        result.DryRun.Should().BeTrue();
        result.CreatedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);

        var created = result.Records[0];
        created.Outcome.Should().Be(SubscriptionImportOutcome.Created);
        created.SubscriptionId.Should().BeNull();
        created.Status.Should().Be(SubscriptionStatus.Active);
        created.NextOrderDate.Should().NotBeNull();

        result.Records[1].Messages.Should().Contain("The customer is required.");

        _subscriptionServiceMock.Verify(x => x.SaveChangesAsync(It.IsAny<IList<Subscription>>()), Times.Never);
    }

    // Next order date

    [Fact]
    public async Task ImportAsync_NextOrderDateInPast_MovesItPastTheReminderWindow()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var record = CreateRecord(nextOrderDate: before.AddDays(-20));

        // Act
        var result = await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        var subscription = _saved.Should().ContainSingle().Subject;
        // Tomorrow plus the default reminder lead time (3 days), so the reminder goes out before the first charge.
        subscription.NextOrderDate.Should().BeOnOrAfter(before.AddDays(4)).And.BeOnOrBefore(DateTime.UtcNow.AddDays(4));
        result.Records.Single().Messages.Should().Contain(x => x.Contains("too soon for a reminder"));
    }

    [Fact]
    public async Task ImportAsync_NoNextOrderDate_AdvancesFromLastOrderDateIntoTheFuture()
    {
        // Arrange
        var lastOrderDate = DateTime.UtcNow.AddDays(-45);
        var record = CreateRecord(useDefaultNextOrderDate: false);
        record.IntervalUnit = SubscriptionIntervalUnit.Week;
        record.IntervalCount = 4;
        record.LastOrderDate = lastOrderDate;

        // Act
        await CreateSut().ImportAsync([record], dryRun: false);

        // Assert: 28 days later is in the past, 56 days later is the next date.
        _saved.Single().NextOrderDate.Should().BeCloseTo(lastOrderDate.AddDays(56), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ImportAsync_UnspecifiedKindDates_AreTreatedAsUtc()
    {
        // Arrange
        var next = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(5), DateTimeKind.Unspecified);
        var record = CreateRecord(nextOrderDate: next);

        // Act
        await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        var subscription = _saved.Single();
        subscription.NextOrderDate.Kind.Should().Be(DateTimeKind.Utc);
        subscription.NextOrderDate.Ticks.Should().Be(next.Ticks);
    }

    // Plan matching

    [Fact]
    public async Task ImportAsync_PlanOffersLegacyFrequency_LinksPlanAndTakesItsDiscount()
    {
        // Arrange
        _offers[ProductId] = CreateOffer((SubscriptionIntervalUnit.Week, 2), (SubscriptionIntervalUnit.Month, 1));

        // Act
        var result = await CreateSut().ImportAsync([CreateRecord()], dryRun: false);

        // Assert
        var subscription = _saved.Should().ContainSingle().Subject;
        subscription.PlanId.Should().Be("plan-1");
        subscription.PlanName.Should().Be("Subscribe & Save");
        subscription.DiscountType.Should().Be(SubscriptionDiscountType.Percent);
        subscription.DiscountValue.Should().Be(10);
        result.Records.Single().PlanId.Should().Be("plan-1");
        result.Records.Single().Messages.Should().BeEmpty();

        _offerServiceMock.Verify(x => x.GetOffersAsync(StoreId, It.Is<IList<string>>(ids => ids.SequenceEqual(new[] { ProductId }))), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_PlanDoesNotOfferLegacyFrequency_ImportsWithoutPlanKeepingFrequency()
    {
        // Arrange
        _offers[ProductId] = CreateOffer((SubscriptionIntervalUnit.Week, 2));
        var record = CreateRecord();
        record.IntervalUnit = SubscriptionIntervalUnit.Day;
        record.IntervalCount = 45;

        // Act
        var result = await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        var subscription = _saved.Should().ContainSingle().Subject;
        subscription.PlanId.Should().BeNull();
        subscription.IntervalUnit.Should().Be(SubscriptionIntervalUnit.Day);
        subscription.IntervalCount.Should().Be(45);
        subscription.DiscountType.Should().Be(SubscriptionDiscountType.None);
        subscription.DiscountValue.Should().Be(0);
        result.Records.Single().Messages.Should().Contain(x => x.Contains("doesn't offer every 45 days"));
    }

    [Fact]
    public async Task ImportAsync_NoPlanForProduct_ImportsWithoutPlan()
    {
        // Act
        var result = await CreateSut().ImportAsync([CreateRecord()], dryRun: false);

        // Assert
        _saved.Single().PlanId.Should().BeNull();
        result.Records.Single().Messages.Should().Contain(x => x.Contains("No active plan"));
    }

    [Fact]
    public async Task ImportAsync_RecordDiscount_WinsOverPlanDiscount()
    {
        // Arrange
        _offers[ProductId] = CreateOffer((SubscriptionIntervalUnit.Month, 1));
        var record = CreateRecord();
        record.DiscountType = SubscriptionDiscountType.FixedAmount;
        record.DiscountValue = 2.5m;

        // Act
        await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        var subscription = _saved.Single();
        subscription.PlanId.Should().Be("plan-1");
        subscription.DiscountType.Should().Be(SubscriptionDiscountType.FixedAmount);
        subscription.DiscountValue.Should().Be(2.5m);
    }

    // Validation

    [Fact]
    public async Task ImportAsync_MissingRequiredValues_FailsWithAllReasons()
    {
        // Arrange
        var record = new LegacySubscriptionImportRecord { Quantity = 0, IntervalCount = 0 };

        // Act
        var result = await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        result.FailedCount.Should().Be(1);
        result.Records.Single().Messages.Should().BeEquivalentTo(
            "The legacy subscription id (or OuterId) is required.",
            "The store is required.",
            "The customer is required.",
            "The product is required: the legacy subscription has no products in its order prototype.",
            "The quantity must be between 1 and 9999.",
            "The frequency must be between 1 and 365.");
        _storeServiceMock.Verify(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_UnknownStore_Fails()
    {
        // Arrange
        var record = CreateRecord();
        record.StoreId = "missing-store";

        // Act
        var result = await CreateSut().ImportAsync([record], dryRun: false);

        // Assert
        result.Records.Single().Outcome.Should().Be(SubscriptionImportOutcome.Failed);
        result.Records.Single().Messages.Should().Contain("Store missing-store was not found.");
        _saved.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportAsync_UnknownProduct_FailsActiveButImportsCancelled()
    {
        // Arrange
        var active = CreateRecord();
        active.ProductId = "deleted-product";
        var cancelled = CreateRecord();
        cancelled.LegacySubscriptionId = "sub-2";
        cancelled.ProductId = "deleted-product";
        cancelled.Status = LegacySubscriptionStatus.Cancelled;

        // Act
        var result = await CreateSut().ImportAsync([active, cancelled], dryRun: false);

        // Assert
        result.Records[0].Outcome.Should().Be(SubscriptionImportOutcome.Failed);
        result.Records[0].Messages.Should().Contain("Product deleted-product was not found.");
        result.Records[1].Outcome.Should().Be(SubscriptionImportOutcome.Created);
        _saved.Should().ContainSingle().Which.OuterId.Should().Be("legacy:sub-2");
    }

    [Fact]
    public async Task ImportAsync_BatchSaveFails_SavesOneByOneAndReportsTheFailure()
    {
        // Arrange
        var bad = CreateRecord();
        bad.LegacySubscriptionId = "sub-bad";
        var calls = 0;
        _subscriptionServiceMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<Subscription>>()))
            .Returns((IList<Subscription> models) =>
            {
                calls++;
                if (models.Count > 1 || models[0].OuterId == "legacy:sub-bad")
                {
                    throw new InvalidOperationException("Database error");
                }

                _saved.AddRange(models);
                return Task.CompletedTask;
            });

        // Act
        var result = await CreateSut().ImportAsync([CreateRecord(), bad], dryRun: false);

        // Assert
        calls.Should().Be(3);
        result.CreatedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Records[1].Messages.Should().Contain("Database error");
        _saved.Should().ContainSingle().Which.OuterId.Should().Be("legacy:sub-1");
    }

    [Fact]
    public async Task ImportAsync_NoRecords_ReturnsEmptyResult()
    {
        // Act
        var result = await CreateSut().ImportAsync([], dryRun: false);

        // Assert
        result.TotalCount.Should().Be(0);
        result.Records.Should().BeEmpty();
        _subscriptionSearchServiceMock.VerifyNoOtherCalls();
    }

    private static LegacySubscriptionImportRecord CreateRecord(DateTime? nextOrderDate = null, bool useDefaultNextOrderDate = true)
    {
        return new LegacySubscriptionImportRecord
        {
            LegacySubscriptionId = "sub-1",
            LegacySubscriptionNumber = "SU-OLD-1",
            StoreId = StoreId,
            CustomerId = "customer-1",
            CustomerName = "Jane Doe",
            ProductId = ProductId,
            Quantity = 2,
            IntervalUnit = SubscriptionIntervalUnit.Month,
            IntervalCount = 1,
            Status = LegacySubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            NextOrderDate = nextOrderDate ?? (useDefaultNextOrderDate ? DateTime.UtcNow.AddDays(7) : null),
            PaymentMethodCode = "AuthorizeNet",
            ShippingAddress = new Address { AddressType = AddressType.Shipping, Line1 = "1 Main St", City = "Springfield", CountryCode = "USA" },
        };
    }

    private static SubscriptionOffer CreateOffer(params (SubscriptionIntervalUnit Unit, int Count)[] intervals)
    {
        return new SubscriptionOffer
        {
            ProductId = ProductId,
            PlanId = "plan-1",
            PlanName = "Subscribe & Save",
            DiscountType = SubscriptionDiscountType.Percent,
            DiscountValue = 10,
            Intervals = intervals.Select(x => new SubscriptionPlanInterval { Unit = x.Unit, Count = x.Count }).ToList(),
        };
    }
}
