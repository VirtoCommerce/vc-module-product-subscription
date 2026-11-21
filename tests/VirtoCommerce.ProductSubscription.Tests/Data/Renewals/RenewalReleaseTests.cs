using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>A renewal that ends lets go of its subscriptions, so the scheduler plans their next delivery.</summary>
[Trait("Category", "Unit")]
public class RenewalReleaseTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task RenewalCancelledBecauseItsSubscriptionWentPastDue_ReleasesTheSubscription()
    {
        var renewalId = Guid.NewGuid().ToString("N");
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x =>
        {
            x.Status = SubscriptionStatus.PastDue;
            x.CurrentRenewalId = renewalId;
        });
        _harness.AddRenewal(NewRenewal(renewalId, subscription));

        await _harness.Processor.ProcessAsync(renewalId);

        _harness.GetRenewal(renewalId).State.Should().Be(RenewalState.Cancelled);
        _harness.GetSubscription(subscription.Id).CurrentRenewalId.Should().BeNull();
    }

    [Fact]
    public async Task SubscriptionReactivatedAfterItsRenewalWasCancelled_IsRenewedAgain()
    {
        var renewalId = Guid.NewGuid().ToString("N");
        _harness.Card.CustomerId = "customer-1";
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x =>
        {
            x.Status = SubscriptionStatus.PastDue;
            x.StoredPaymentMethodId = null;
            x.CurrentRenewalId = renewalId;
        });
        _harness.AddRenewal(NewRenewal(renewalId, subscription));
        await _harness.Processor.ProcessAsync(renewalId);

        // The customer picks a card, which makes a past-due subscription active again.
        await _harness.Manager.ChangeAsync(subscription.Id, new SubscriptionChange { StoredPaymentMethodId = _harness.Card.Id });
        _harness.GetSubscription(subscription.Id).Status.Should().Be(SubscriptionStatus.Active);

        await _harness.Processor.ProcessDueAsync();

        _harness.GetRenewalsOf(subscription.Id).Should().ContainSingle(x => x.Id != renewalId);
    }

    [Fact]
    public async Task SkippedRenewal_ReleasesEverySubscription()
    {
        var renewalId = Guid.NewGuid().ToString("N");
        var first = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x => x.CurrentRenewalId = renewalId);
        var second = _harness.AddSubscription("customer-1", _harness.Now.AddDays(1), x => x.CurrentRenewalId = renewalId);
        _harness.AddRenewal(NewRenewal(renewalId, first, second));

        await _harness.Processor.SkipAsync(renewalId);

        new[] { first, second }.Select(x => _harness.GetSubscription(x.Id).CurrentRenewalId).Should().AllBeEquivalentTo((string)null);
    }

    private Renewal NewRenewal(string id, params Subscription[] subscriptions)
    {
        var renewal = new Renewal
        {
            Id = id,
            Number = $"REN-{id[..6]}",
            StoreId = RenewalTestHarness.StoreId,
            CustomerId = subscriptions[0].CustomerId,
            Currency = "USD",
            State = RenewalState.Scheduled,
            ScheduledDate = _harness.Now.AddDays(1),
            CutoffDate = _harness.Now.AddDays(1),
            NextProcessingDate = _harness.Now,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Items = subscriptions.Select(x => new RenewalItem { SubscriptionId = x.Id, ProductId = x.ProductId, ProductName = x.ProductName, Quantity = x.Quantity }).ToList(),
        };

        return renewal;
    }
}
