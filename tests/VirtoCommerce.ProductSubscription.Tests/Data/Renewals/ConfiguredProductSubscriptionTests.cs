using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using VirtoCommerce.ProductSubscription.Tests.Core;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// A configurable product: the subscription keeps the options of the first order, every renewal orders the same options,
/// and a renewal whose options can't be ordered any more asks the customer instead of ordering something else.
/// </summary>
[Trait("Category", "Unit")]
public class ConfiguredProductSubscriptionTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task CreateFromOrder_KeepsTheConfigurationOfTheOrderLine()
    {
        var line = SubscriptionConfigurationTests.ConfiguredOrderLine();
        SetupChoices(Choice(line));

        var subscription = (await _harness.Manager.CreateFromOrderAsync(Order(line))).Should().ContainSingle().Subject;

        subscription.ProductId.Should().Be("gift-box");
        subscription.Configuration.Should().NotBeNull();
        subscription.Configuration.CapturedFrom.OrderNumber.Should().Be("CO-1");
        subscription.Configuration.Sections.Select(x => x.SectionId).Should().Equal("sec-drinks", "sec-card", "sec-logo");
        subscription.ConfigurationHash.Should().Be(subscription.Configuration.ComputeHash());
        subscription.ReferencePrice.Should().Be(24.60m);
    }

    [Fact]
    public async Task CreateFromOrder_MatchesTwoLinesOfOneProductByTheirFingerprint()
    {
        var fanta = SubscriptionConfigurationTests.ConfiguredOrderLine("line-fanta", "fanta-mango");
        var cola = SubscriptionConfigurationTests.ConfiguredOrderLine("line-cola", "cola");
        // The choices come in the other order than the lines: matching by product alone would swap the configurations.
        SetupChoices(Choice(cola, interval: 2), Choice(fanta, interval: 1));

        var subscriptions = await _harness.Manager.CreateFromOrderAsync(Order(fanta, cola));

        subscriptions.Should().HaveCount(2);
        subscriptions.Single(x => x.IntervalCount == 2).Configuration.Sections[0].ProductId.Should().Be("cola");
        subscriptions.Single(x => x.IntervalCount == 1).Configuration.Sections[0].ProductId.Should().Be("fanta-mango");
    }

    [Fact]
    public async Task CreateFromOrder_AnOutdatedFingerprintNeverTakesAnotherChoicesLine()
    {
        var fanta = SubscriptionConfigurationTests.ConfiguredOrderLine("line-fanta", "fanta-mango");
        var cola = SubscriptionConfigurationTests.ConfiguredOrderLine("line-cola", "cola");
        // The first choice has an outdated fingerprint; matched first, it would take the Fanta line the second one names.
        var outdated = Choice(cola, interval: 2);
        outdated.ConfigurationHash = "outdated";
        SetupChoices(outdated, Choice(fanta, interval: 1));

        var subscriptions = await _harness.Manager.CreateFromOrderAsync(Order(fanta, cola));

        subscriptions.Should().HaveCount(2);
        subscriptions.Single(x => x.IntervalCount == 1).Configuration.Sections[0].ProductId.Should().Be("fanta-mango");
        subscriptions.Single(x => x.IntervalCount == 2).Configuration.Sections[0].ProductId.Should().Be("cola");
    }

    [Fact]
    public async Task CreateFromOrder_LeavesASimpleProductWithoutConfiguration()
    {
        var line = new LineItem { Id = "line-1", ProductId = "coffee", Quantity = 2, PlacedPrice = 9m };
        SetupChoices(new SubscriptionCartItem { Id = "choice-1", CartId = "cart-1", LineItemId = "cart-line", ProductId = "coffee", PlanId = "plan-1", IntervalCount = 1 });

        var subscription = (await _harness.Manager.CreateFromOrderAsync(Order(line))).Should().ContainSingle().Subject;

        subscription.Configuration.Should().BeNull();
        subscription.ConfigurationHash.Should().BeNull();
    }

    [Fact]
    public async Task Renewal_CarriesTheConfigurationOfTheSubscription()
    {
        var subscription = AddConfiguredSubscription();

        await _harness.Processor.ProcessDueAsync();

        var item = _harness.GetRenewalsOf(subscription.Id).Single().Items.Single();
        item.ConfigurationHash.Should().Be(subscription.ConfigurationHash);
        item.Configuration.Sections.Should().HaveCount(3);
    }

    [Fact]
    public async Task ChangedConfiguration_AsksForAReviewWithItsCode_AndNeverOrders()
    {
        _harness.CartBuilder
            .Setup(x => x.BuildAsync(It.IsAny<Renewal>(), It.IsAny<IList<Subscription>>()))
            .ReturnsAsync((Renewal renewal, IList<Subscription> _) =>
            {
                var result = new RenewalCartResult();
                foreach (var item in renewal.Items)
                {
                    var copy = (RenewalItem)item.Clone();
                    copy.IsAvailable = false;
                    copy.IsConfigurationChanged = true;
                    copy.Configuration.Sections[0].IsAvailable = false;
                    copy.Message = RenewalConfiguredLineBuilder.ConfigurationChangedMessage;
                    result.Items.Add(copy);
                }

                return result;
            });
        var subscription = AddConfiguredSubscription();

        await _harness.Processor.ProcessDueAsync();

        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        renewal.State.Should().Be(RenewalState.ReviewRequired);
        renewal.ReviewReasonCode.Should().Be(RenewalReviewReasonCode.ConfigurationChanged);
        renewal.ReviewReason.Should().Contain("Office box");
        renewal.Items.Single().IsConfigurationChanged.Should().BeTrue();
        renewal.Items.Single().Configuration.Sections[0].IsAvailable.Should().BeFalse();
        renewal.OrderId.Should().BeNull();
        _harness.CartBuilder.Verify(x => x.PlaceOrderAsync(It.IsAny<Renewal>(), It.IsAny<RenewalCartResult>()), Times.Never);
        _harness.Notifications.Verify(x => x.SendAsync<RenewalReviewRequiredEmailNotification>(It.IsAny<IList<Subscription>>(), It.IsAny<Renewal>()), Times.Once);
    }

    [Fact]
    public void ConfigurationChange_IsReportedBeforeOtherUnavailableItems()
    {
        var renewal = new Renewal { Items = [new RenewalItem { ProductName = "Office box", IsAvailable = false, IsConfigurationChanged = true }, new RenewalItem { ProductName = "Coffee", IsAvailable = false }] };
        var cart = new RenewalCartResult { Items = renewal.Items };

        _harness.Processor.ReviewReasonOf(renewal, cart).Code.Should().Be(RenewalReviewReasonCode.ConfigurationChanged);
    }

    private Subscription AddConfiguredSubscription()
    {
        var configuration = SubscriptionConfigurationMapper.FromOrderLine(SubscriptionConfigurationTests.ConfiguredOrderLine(), "order-1", "CO-1");
        return _harness.AddSubscription("customer-1", _harness.Now, s =>
        {
            s.ProductId = "gift-box";
            s.ProductName = "Office box";
            s.Configuration = configuration;
            s.ConfigurationHash = configuration.ComputeHash();
        });
    }

    private void SetupChoices(params SubscriptionCartItem[] choices)
    {
        _harness.CartItemSearchService
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionCartItemSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((SubscriptionCartItemSearchCriteria criteria, bool _) => new SubscriptionCartItemSearchResult
            {
                TotalCount = choices.Length,
                Results = criteria.Skip == 0 ? choices.ToList() : [],
            });
        _harness.PlanService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync([new SubscriptionPlan { Id = "plan-1", Name = "Monthly", IsActive = true, DiscountType = SubscriptionDiscountType.Percent, DiscountValue = 10 }]);
    }

    private static SubscriptionCartItem Choice(LineItem line, int interval = 1)
    {
        return new SubscriptionCartItem
        {
            Id = Guid.NewGuid().ToString("N"),
            CartId = "cart-1",
            LineItemId = "cart-" + line.Id,
            ProductId = line.ProductId,
            ConfigurationHash = SubscriptionConfigurationMapper.GetHash(line),
            PlanId = "plan-1",
            IntervalUnit = SubscriptionIntervalUnit.Month,
            IntervalCount = interval,
        };
    }

    private CustomerOrder Order(params LineItem[] lines)
    {
        return new CustomerOrder
        {
            Id = "order-1",
            Number = "CO-1",
            StoreId = RenewalTestHarness.StoreId,
            CustomerId = "customer-1",
            ShoppingCartId = "cart-1",
            Currency = "USD",
            CreatedDate = _harness.Now,
            Items = lines.ToList(),
        };
    }
}
