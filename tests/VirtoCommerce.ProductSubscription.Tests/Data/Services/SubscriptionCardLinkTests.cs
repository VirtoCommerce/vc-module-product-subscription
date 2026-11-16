using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Tests.Data.Renewals;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Services;

/// <summary>
/// The card saved with the checkout order pays the order's subscriptions, even when it is saved after they were activated
/// with another card of the customer.
/// </summary>
[Trait("Category", "Unit")]
public class SubscriptionCardLinkTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task CardSavedWithTheOrder_ReplacesTheCardTheSubscriptionsWereActivatedWith()
    {
        var first = _harness.AddSubscription("customer-1", _harness.Now.AddMonths(1), x => x.InitialOrderId = "order-1");
        var second = _harness.AddSubscription("customer-1", _harness.Now.AddMonths(1), x => x.InitialOrderId = "order-1");

        var linked = await _harness.Manager.LinkStoredPaymentMethodAsync(NewCard("customer-1", "order-1"));

        linked.Should().HaveCount(2);
        _harness.GetSubscription(first.Id).StoredPaymentMethodId.Should().Be("card-new");
        _harness.GetSubscription(second.Id).StoredPaymentMethodId.Should().Be("card-new");
        _harness.GetSubscription(first.Id).PaymentMethodCode.Should().Be("NewCardMethod");
    }

    [Fact]
    public async Task SubscriptionsOfOtherOrdersOrCustomersOrCancelled_KeepTheirCard()
    {
        var otherOrder = _harness.AddSubscription("customer-1", _harness.Now.AddMonths(1), x => x.InitialOrderId = "order-2");
        var otherCustomer = _harness.AddSubscription("customer-2", _harness.Now.AddMonths(1), x => x.InitialOrderId = "order-1");
        var cancelled = _harness.AddSubscription("customer-1", _harness.Now.AddMonths(1), x =>
        {
            x.InitialOrderId = "order-1";
            x.Status = SubscriptionStatus.Cancelled;
        });

        var linked = await _harness.Manager.LinkStoredPaymentMethodAsync(NewCard("customer-1", "order-1"));

        linked.Should().BeEmpty();
        _harness.GetSubscription(otherOrder.Id).StoredPaymentMethodId.Should().Be(_harness.Card.Id);
        _harness.GetSubscription(otherCustomer.Id).StoredPaymentMethodId.Should().Be(_harness.Card.Id);
        _harness.GetSubscription(cancelled.Id).StoredPaymentMethodId.Should().Be(_harness.Card.Id);
    }

    [Fact]
    public async Task CardNotSavedWithAnOrderOrNotActive_LinksNothing()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddMonths(1), x => x.InitialOrderId = "order-1");

        var withoutOrder = NewCard("customer-1", null);
        var revoked = NewCard("customer-1", "order-1");
        revoked.Status = StoredPaymentMethodStatus.Revoked;

        (await _harness.Manager.LinkStoredPaymentMethodAsync(withoutOrder)).Should().BeEmpty();
        (await _harness.Manager.LinkStoredPaymentMethodAsync(revoked)).Should().BeEmpty();
        _harness.GetSubscription(subscription.Id).StoredPaymentMethodId.Should().Be(_harness.Card.Id);
    }

    private static StoredPaymentMethod NewCard(string customerId, string initialOrderId)
    {
        return new StoredPaymentMethod
        {
            Id = "card-new",
            StoreId = RenewalTestHarness.StoreId,
            CustomerId = customerId,
            PaymentMethodCode = "NewCardMethod",
            InitialOrderId = initialOrderId,
            Status = StoredPaymentMethodStatus.Active,
        };
    }
}
