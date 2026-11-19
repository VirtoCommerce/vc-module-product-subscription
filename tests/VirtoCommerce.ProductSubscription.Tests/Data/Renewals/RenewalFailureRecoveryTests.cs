using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CartModule.Core.Model;

using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using CartAggregate = VirtoCommerce.XCart.Core.CartAggregate;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// A run that stops half-way (a database or network failure) must never order or charge a delivery twice, and a
/// delivery the subscription can't pay is never shipped unpaid.
/// </summary>
[Trait("Category", "Unit")]
public class RenewalFailureRecoveryTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task OrderPlacedButSubscriptionsNotMoved_TheNextRunMovesThemWithoutASecondOrder()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);
        var dueDate = subscription.NextOrderDate;
        var failed = false;
        _harness.FailSubscriptionSave = models => !failed && models.Any(x => x.LastOrderId != null) && (failed = true);

        await _harness.Processor.ProcessDueAsync();

        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        renewal.OrderId.Should().NotBeNull("the order was placed before the failure");
        _harness.GetSubscription(subscription.Id).NextOrderDate.Should().Be(dueDate);

        _harness.Now = _harness.Now.AddHours(1);
        await _harness.Processor.ProcessDueAsync();
        _harness.Now = _harness.Now.AddHours(1);
        await _harness.Processor.ProcessDueAsync();

        var moved = _harness.GetSubscription(subscription.Id);
        moved.NextOrderDate.Should().BeAfter(dueDate);
        moved.LastOrderId.Should().Be(renewal.OrderId);
        _harness.GetRenewalsOf(subscription.Id).Should().ContainSingle("no second renewal is created for the same delivery");
        _harness.PaymentMethod.Requests.Select(x => x.OrderId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task ChargeAcceptedButOrderNotSaved_TheRetrySendsTheSameIdempotencyKey()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);
        var failed = false;
        _harness.FailOrderSave = models => !failed
            && models.Any(o => o.InPayments?.Any(p => p.PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid) == true)
            && (failed = true);

        await _harness.Processor.ProcessDueAsync();

        // The harness keeps orders by reference; in the database the failed save left the payment as it was.
        var orderId = _harness.GetRenewalsOf(subscription.Id).Single().OrderId;
        foreach (var payment in _harness.GetOrder(orderId).InPayments)
        {
            payment.PaymentStatus = PaymentStatus.New;
            payment.Status = nameof(PaymentStatus.New);
        }

        _harness.Now = _harness.Now.AddHours(1);
        await _harness.Processor.ProcessDueAsync();

        var keys = _harness.PaymentMethod.Requests.Select(x => x.IdempotencyKey).ToList();
        keys.Should().HaveCountGreaterThan(1, "the charge is sent again after the failed save");
        keys.Distinct().Should().ContainSingle("the provider must recognize the repeat, not authorize the card again");
        _harness.GetRenewalsOf(subscription.Id).Single().State.Should().Be(RenewalState.Authorized);
    }

    [Fact]
    public async Task RenewalNotSavedAfterItsSubscriptionsWereLinked_TheNextRunMakesOneRenewal()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);
        var failed = false;
        _harness.FailRenewalSave = models => !failed && models.Any(x => x.Number == null) && (failed = true);

        try
        {
            await _harness.Processor.ProcessDueAsync();
        }
        catch (InvalidOperationException)
        {
            // The failed save may surface from the scheduler run; what matters is the next run.
        }

        _harness.GetRenewalsOf(subscription.Id).Should().BeEmpty();

        _harness.Now = _harness.Now.AddMinutes(5);
        await _harness.Processor.ProcessDueAsync();

        _harness.GetRenewalsOf(subscription.Id).Should().ContainSingle();
        _harness.PaymentMethod.Requests.Select(x => x.OrderId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task LinkingTheSubscriptionsFails_NoOpenRenewalIsLeftBehind()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);
        var failed = false;
        _harness.FailSubscriptionSave = models => !failed && models.Any(x => x.CurrentRenewalId != null && x.LastOrderId == null) && (failed = true);

        try
        {
            await _harness.Processor.ProcessDueAsync();
        }
        catch (InvalidOperationException)
        {
            // The failed save may surface from the scheduler run; what matters is the next run.
        }

        _harness.Now = _harness.Now.AddMinutes(5);
        await _harness.Processor.ProcessDueAsync();

        // Saved before its subscriptions knew about it, a renewal would stay open while the next run made another one.
        _harness.GetRenewalsOf(subscription.Id).Should().ContainSingle();
        _harness.PaymentMethod.Requests.Select(x => x.OrderId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void ACartWithNoPayment_AsksForAReviewInsteadOfShippingUnpaid()
    {
        var cart = new RenewalCartResult
        {
            Aggregate = new CartWithoutPayment(),
            Items = [new RenewalItem { ProductName = "Coffee", IsAvailable = true }],
        };

        var reason = _harness.Processor.ReviewReasonOf(new Renewal { Items = cart.Items }, cart);

        reason.Should().NotBeNull();
        reason.Code.Should().Be(RenewalReviewReasonCode.NoPaymentMethod);
    }

    [Fact]
    public async Task CustomerRetryWithTheSameCard_RespectsTheRetryLimit()
    {
        var renewal = AddFailedRenewal(attempts: 3);
        SetMaxRetries(3);

        var act = () => _harness.Processor.RetryPaymentAsync(renewal.Id);

        (await act.Should().ThrowAsync<SubscriptionOperationException>()).Which.Message.Should().Contain("another card");
        _harness.PaymentMethod.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CustomerRetryWithAnotherCard_IsAllowed()
    {
        var renewal = AddFailedRenewal(attempts: 3, subscriptionCard: "card-2");
        SetMaxRetries(3);

        var act = () => _harness.Processor.RetryPaymentAsync(renewal.Id);

        await act.Should().NotThrowAsync();
        _harness.GetRenewal(renewal.Id).StoredPaymentMethodId.Should().Be("card-2");
    }

    private void SetMaxRetries(int value)
    {
        _harness.Store.Settings.Add(new ObjectSettingEntry { Name = ModuleConstants.Settings.Store.MaxRetriesPer30Days.Name, Value = value });
    }

    private Renewal AddFailedRenewal(int attempts, string subscriptionCard = null)
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(10), s =>
        {
            if (subscriptionCard != null)
            {
                s.StoredPaymentMethodId = subscriptionCard;
            }
        });

        var renewal = new Renewal
        {
            Id = Guid.NewGuid().ToString("N"),
            Number = "REN-RETRY",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            StoreId = RenewalTestHarness.StoreId,
            CustomerId = "customer-1",
            Currency = "USD",
            State = RenewalState.Failed,
            OrderId = "order-1",
            StoredPaymentMethodId = _harness.Card.Id,
            PaymentMethodCode = _harness.PaymentMethod.Code,
            Items = [new RenewalItem { SubscriptionId = subscription.Id, ProductId = subscription.ProductId, Quantity = 1, IsAvailable = true }],
        };

        for (var i = 0; i < attempts; i++)
        {
            renewal.AddEvent(RenewalEventType.PaymentAttempt, $"Payment attempt {i + 1}: SoftDecline");
        }

        _harness.AddRenewal(renewal);
        return renewal;
    }

    /// <summary>A renewal cart with a line and a shipment but no payment: the subscription's payment method is gone.</summary>
    private sealed class CartWithoutPayment : CartAggregate
    {
        public CartWithoutPayment()
            : base(null, null, null, null, null, null, null, null, null, null, null, null)
        {
            Cart = new ShoppingCart
            {
                Currency = "USD",
                Items = new List<LineItem> { new() { Id = "line-1", ProductId = "product-1", Quantity = 1 } },
                Shipments = new List<Shipment> { new() { ShipmentMethodCode = "FixedRate" } },
                Payments = new List<Payment>(),
            };
        }
    }
}
