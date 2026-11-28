using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Notifications;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// A manual process or retry call makes one charge attempt. After a soft decline the renewal waits for its scheduled retry,
/// even though the manual call ignores the schedule.
/// </summary>
[Trait("Category", "Unit")]
public class RenewalSingleChargeTests
{
    private readonly RenewalTestHarness _harness = new();

    public RenewalSingleChargeTests()
    {
        _harness.PaymentMethod.Respond = _ => new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.SoftDecline };
    }

    [Fact]
    public async Task ManualProcess_SoftDecline_ChargesOnce()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now.AddDays(2));
        await _harness.Processor.ProcessDueAsync();
        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();

        await _harness.Processor.ProcessAsync(renewal.Id, ignoreSchedule: true);

        _harness.PaymentMethod.Requests.Should().ContainSingle();
        renewal = _harness.GetRenewal(renewal.Id);
        renewal.State.Should().Be(RenewalState.RetryScheduled);
        renewal.NextAttemptDate.Should().NotBeNull();
    }

    [Fact]
    public async Task RetryPayment_SoftDecline_ChargesOnce_AndKeepsTheRetrySchedule()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);
        await _harness.Processor.ProcessDueAsync();
        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        _harness.GetRenewal(renewal.Id).State.Should().Be(RenewalState.RetryScheduled);
        _harness.PaymentMethod.Requests.Should().ContainSingle();

        await _harness.Processor.RetryPaymentAsync(renewal.Id);

        _harness.PaymentMethod.Requests.Should().HaveCount(2);
        renewal = _harness.GetRenewal(renewal.Id);
        renewal.State.Should().Be(RenewalState.RetryScheduled);
        _harness.GetSubscription(subscription.Id).Status.Should().NotBe(SubscriptionStatus.PastDue);
    }

    [Fact]
    public async Task RetryPayment_SoftDecline_TellsTheCustomer_ButAutomaticRetriesStayQuiet()
    {
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);
        await _harness.Processor.ProcessDueAsync();
        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        // the first decline is told
        VerifyPaymentFailedEmails(Times.Once());

        // an automatic retry that is declined again stays quiet
        _harness.Now = _harness.GetRenewal(renewal.Id).NextAttemptDate!.Value.AddMinutes(1);
        await _harness.Processor.ProcessDueAsync();
        _harness.PaymentMethod.Requests.Should().HaveCount(2);
        VerifyPaymentFailedEmails(Times.Once());

        // the customer asks for a retry and waits for its result: its decline is told
        await _harness.Processor.RetryPaymentAsync(renewal.Id);
        _harness.PaymentMethod.Requests.Should().HaveCount(3);
        VerifyPaymentFailedEmails(Times.Exactly(2));
    }

    private void VerifyPaymentFailedEmails(Times times)
    {
        _harness.Notifications.Verify(
            x => x.SendAsync(It.IsAny<IList<Subscription>>(), It.IsAny<Renewal>(), It.IsAny<Action<RenewalPaymentFailedEmailNotification>>()),
            times);
    }
}
