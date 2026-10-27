using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.ProductSubscription.Core.Models;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// When the provider retries a declined charge on its own, the platform must not charge again
/// (<c>StoredCredentialResultBase.IsProviderRetryScheduled</c>): it waits for the result on the order.
/// </summary>
[Trait("Category", "Unit")]
public class RenewalProviderRetryTests
{
    private readonly RenewalTestHarness _harness = new();
    private readonly Subscription _subscription;

    public RenewalProviderRetryTests()
    {
        _harness.PaymentMethod.Respond = _ => new ProcessStoredPaymentRequestResult
        {
            Outcome = ChargeOutcome.SoftDecline,
            IsProviderRetryScheduled = true,
        };

        _subscription = _harness.AddSubscription("customer-1", _harness.Now);
    }

    [Fact]
    public async Task ProviderRetry_IsNeverChargedAgain_AndFailsAfterTheWait()
    {
        await _harness.Processor.ProcessDueAsync();

        var renewal = _harness.GetRenewalsOf(_subscription.Id).Single();
        renewal.State.Should().Be(RenewalState.RetryScheduled);
        renewal.NextAttemptDate.Should().BeNull();
        _harness.PaymentMethod.Requests.Should().ContainSingle();

        // The old code charged again after 2 days, while the provider was retrying too.
        _harness.Now = _harness.Now.AddDays(3);
        await _harness.Processor.ProcessDueAsync();
        _harness.PaymentMethod.Requests.Should().ContainSingle();
        _harness.GetRenewal(renewal.Id).State.Should().Be(RenewalState.RetryScheduled);

        _harness.Now = _harness.Now.AddDays(12);
        await _harness.Processor.ProcessDueAsync();
        _harness.PaymentMethod.Requests.Should().ContainSingle();
        _harness.GetRenewal(renewal.Id).State.Should().Be(RenewalState.Failed);
        _harness.GetSubscription(_subscription.Id).Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Fact]
    public async Task ProviderRetry_CompletesWhenTheProviderCollectsThePayment()
    {
        await _harness.Processor.ProcessDueAsync();
        var renewal = _harness.GetRenewalsOf(_subscription.Id).Single();

        var order = _harness.GetOrder(renewal.OrderId);
        order.InPayments.Single().PaymentStatus = PaymentStatus.Authorized;
        await _harness.Processor.HandleOrderChangedAsync(order);

        _harness.GetRenewal(renewal.Id).State.Should().Be(RenewalState.Authorized);
        _harness.PaymentMethod.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ProviderRetry_FailsWhenTheProviderGivesUp()
    {
        await _harness.Processor.ProcessDueAsync();
        var renewal = _harness.GetRenewalsOf(_subscription.Id).Single();

        var order = _harness.GetOrder(renewal.OrderId);
        order.InPayments.Single().PaymentStatus = PaymentStatus.Declined;
        await _harness.Processor.HandleOrderChangedAsync(order);

        _harness.GetRenewal(renewal.Id).State.Should().Be(RenewalState.Failed);
    }

    [Fact]
    public async Task ProviderRetry_CannotBeRetriedByTheCustomer()
    {
        await _harness.Processor.ProcessDueAsync();
        var renewal = _harness.GetRenewalsOf(_subscription.Id).Single();

        var retry = () => _harness.Processor.RetryPaymentAsync(renewal.Id);

        await retry.Should().ThrowAsync<InvalidOperationException>().WithMessage("*provider is retrying*");
        _harness.PaymentMethod.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task OwnSoftDeclineRetry_StillChargesAgainOnSchedule()
    {
        _harness.PaymentMethod.Respond = request => request.Reason == MerchantInitiatedReason.Resubmission
            ? new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.Succeeded }
            : new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.SoftDecline };

        await _harness.Processor.ProcessDueAsync();
        var renewal = _harness.GetRenewalsOf(_subscription.Id).Single();
        _harness.GetRenewal(renewal.Id).NextAttemptDate.Should().NotBeNull();

        _harness.Now = _harness.Now.AddDays(2);
        await _harness.Processor.ProcessDueAsync();

        _harness.PaymentMethod.Requests.Should().HaveCount(2);
        _harness.GetRenewal(renewal.Id).State.Should().Be(RenewalState.Authorized);
    }
}
