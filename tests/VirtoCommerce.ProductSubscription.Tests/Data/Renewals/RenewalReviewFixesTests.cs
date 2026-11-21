using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>Renewal and subscription behavior fixed after the review of the module's first pull request.</summary>
[Trait("Category", "Unit")]
public class RenewalReviewFixesTests
{
    private readonly RenewalTestHarness _harness = new();

    public RenewalReviewFixesTests()
    {
        // Noon, so a few hours either way stay on the same day.
        _harness.Now = DateTime.UtcNow.Date.AddHours(12);
    }

    [Fact]
    public async Task PaymentActionEmail_LinksToTheProvidersRecoveryPage_WhenItGivesOne()
    {
        _harness.PaymentMethod.Respond = _ => new ProcessStoredPaymentRequestResult
        {
            Outcome = ChargeOutcome.RequiresAction,
            RecoveryUrl = "https://psp.example.com/authenticate/abc",
        };
        var paymentUrl = CapturePaymentActionUrl();
        _harness.AddSubscription("customer-1", _harness.Now);

        await _harness.Processor.ProcessDueAsync();

        paymentUrl().Should().Be("https://psp.example.com/authenticate/abc");
    }

    [Fact]
    public async Task PaymentActionEmail_LinksToTheOrderPaymentPage_WithoutARecoveryPage()
    {
        _harness.PaymentMethod.Respond = _ => new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.RequiresAction };
        _harness.Notifications.Setup(x => x.GetStoreUrlAsync(RenewalTestHarness.StoreId)).ReturnsAsync("https://shop.example.com/");
        var paymentUrl = CapturePaymentActionUrl();
        _harness.AddSubscription("customer-1", _harness.Now);

        await _harness.Processor.ProcessDueAsync();

        paymentUrl().Should().StartWith("https://shop.example.com/account/orders/").And.EndWith("/payment");
    }

    [Fact]
    public async Task RenewalOfADeletedStore_IsCancelledOnce_AndReleasesItsSubscription()
    {
        var renewalId = Guid.NewGuid().ToString("N");
        var subscription = _harness.AddSubscription("customer-1", _harness.Now, x => x.CurrentRenewalId = renewalId);
        _harness.AddRenewal(new Renewal
        {
            Id = renewalId,
            StoreId = RenewalTestHarness.StoreId,
            CustomerId = subscription.CustomerId,
            State = RenewalState.Scheduled,
            NextProcessingDate = _harness.Now,
            Items = [new RenewalItem { SubscriptionId = subscription.Id, ProductId = subscription.ProductId, Quantity = 1 }],
        });
        _harness.StoreDeleted = true;

        await _harness.Processor.ProcessAsync(renewalId);
        await _harness.Processor.ProcessAsync(renewalId);

        // It used to fail on every scheduler run, adding an error event each time.
        var renewal = _harness.GetRenewal(renewalId);
        renewal.State.Should().Be(RenewalState.Cancelled);
        renewal.Events.Should().ContainSingle(x => x.EventType == RenewalEventType.Error);
        _harness.GetSubscription(subscription.Id).CurrentRenewalId.Should().BeNull();
    }

    [Fact]
    public async Task Resume_LeavesTheReminderItsLeadDays()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(-5), x =>
        {
            x.Status = SubscriptionStatus.Paused;
            x.PausedUntil = _harness.Now.AddHours(-1);
        });

        await _harness.Processor.ProcessDueAsync();

        // Lead days 3: the reminder goes out tomorrow at the earliest, never in the same run as the order.
        _harness.GetSubscription(subscription.Id).NextOrderDate.Should().Be(_harness.Now.AddDays(4));
        _harness.GetRenewalsOf(subscription.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task CustomerResume_LeavesTheReminderItsLeadDays()
    {
        var subscription = _harness.AddSubscription("customer-1", DateTime.UtcNow.AddDays(-5), x => x.Status = SubscriptionStatus.Paused);

        var resumed = await _harness.Manager.ResumeAsync(subscription.Id);

        resumed.NextOrderDate.Should().BeOnOrAfter(DateTime.UtcNow.AddDays(4).AddMinutes(-1));
    }

    [Fact]
    public async Task SameProductWithDifferentDiscounts_GoesToSeparateRenewals()
    {
        var plain = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x => x.DiscountValue = 5);
        var better = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x => x.DiscountValue = 10);
        var sameAsPlain = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x => x.DiscountValue = 5);

        await _harness.Processor.ProcessDueAsync();

        var renewals = _harness.GetRenewals();
        renewals.Should().HaveCount(2);
        renewals.Should().ContainSingle(x => x.Items.Select(i => i.SubscriptionId).OrderBy(i => i).SequenceEqual(new[] { plain.Id, sameAsPlain.Id }.OrderBy(i => i)));
        renewals.Should().ContainSingle(x => x.Items.Count == 1 && x.Items[0].SubscriptionId == better.Id);
    }

    [Fact]
    public async Task OneCustomersSameDayGroup_SplitByAPageBoundary_StillMakesOneRenewal()
    {
        for (var i = 0; i < 499; i++)
        {
            _harness.AddSubscription($"customer-{i}", _harness.Now.AddHours(-2));
        }

        // Positions 500 and 501 of the scheduler's search: the last row of the first page and the first of the second.
        var first = _harness.AddSubscription("customer-split", _harness.Now.AddHours(-1));
        var second = _harness.AddSubscription("customer-split", _harness.Now.AddMinutes(-30), x => x.ProductId = "product-2");

        await _harness.Processor.ProcessDueAsync();

        _harness.GetRenewalsOf(first.Id).Should().ContainSingle()
            .Which.Items.Select(x => x.SubscriptionId).Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task SubscriptionsFromTwoLinesOfOneProduct_KeepTheirOwnQuantity()
    {
        var plan = new SubscriptionPlan { Id = "plan-1", Name = "Monthly", Intervals = [new SubscriptionPlanInterval { Unit = SubscriptionIntervalUnit.Month, Count = 1 }] };
        _harness.PlanService.Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync([plan]);
        _harness.CartItemSearchService
            .Setup(x => x.SearchAsync(It.IsAny<SubscriptionCartItemSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync(new SubscriptionCartItemSearchResult
            {
                Results =
                [
                    new SubscriptionCartItem { Id = "choice-1", CartId = "cart-1", LineItemId = "line-1", ProductId = "product-1", PlanId = plan.Id },
                    new SubscriptionCartItem { Id = "choice-2", CartId = "cart-1", LineItemId = "line-2", ProductId = "product-1", PlanId = plan.Id },
                ],
                TotalCount = 2,
            });
        var order = new CustomerOrder
        {
            Id = "order-1",
            Number = "CO-1",
            StoreId = RenewalTestHarness.StoreId,
            CustomerId = "customer-1",
            ShoppingCartId = "cart-1",
            Items =
            [
                new LineItem { Id = "order-line-1", ProductId = "product-1", Quantity = 2, PlacedPrice = 10 },
                new LineItem { Id = "order-line-2", ProductId = "product-1", Quantity = 5, PlacedPrice = 9 },
            ],
        };

        var subscriptions = await _harness.Manager.CreateFromOrderAsync(order);

        subscriptions.Select(x => (x.Quantity, x.ReferencePrice)).Should().BeEquivalentTo(new[] { (2, (decimal?)10m), (5, (decimal?)9m) });
    }

    private Func<string> CapturePaymentActionUrl()
    {
        var notification = new RenewalPaymentActionRequiredEmailNotification();
        _harness.Notifications
            .Setup(x => x.SendAsync(It.IsAny<IList<Subscription>>(), It.IsAny<Renewal>(), It.IsAny<Action<RenewalPaymentActionRequiredEmailNotification>>()))
            .Callback((IList<Subscription> _, Renewal _, Action<RenewalPaymentActionRequiredEmailNotification> configure) => configure?.Invoke(notification))
            .Returns(Task.CompletedTask);

        return () => notification.PaymentUrl;
    }
}
