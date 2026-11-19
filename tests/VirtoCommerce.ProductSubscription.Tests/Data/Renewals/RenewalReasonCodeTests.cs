using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Data.Renewals;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>
/// The storefront translates a renewal's problem from a stable code; the English text stays for the admin and logs.
/// </summary>
[Trait("Category", "Unit")]
public class RenewalReasonCodeTests
{
    private readonly RenewalTestHarness _harness = new();

    [Fact]
    public async Task SoftDecline_RecordsTheWillRetryCode_AndASuccessfulRetryClearsIt()
    {
        _harness.PaymentMethod.Respond = request => request.Reason == MerchantInitiatedReason.Resubmission
            ? new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.Succeeded }
            : new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.SoftDecline };
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);

        await _harness.Processor.ProcessDueAsync();

        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        renewal.State.Should().Be(RenewalState.RetryScheduled);
        renewal.LastErrorCode.Should().Be(RenewalErrorCode.CardDeclinedWillRetry);
        renewal.LastErrorMessage.Should().NotBeNullOrEmpty();

        _harness.Now = _harness.Now.AddDays(2);
        await _harness.Processor.ProcessDueAsync();

        renewal = _harness.GetRenewal(renewal.Id);
        renewal.State.Should().Be(RenewalState.Authorized);
        renewal.LastErrorCode.Should().BeNull();
        renewal.LastErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task HardDecline_FailsWithTheCardDeclinedCode()
    {
        _harness.PaymentMethod.Respond = _ => new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.HardDecline };
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);

        await _harness.Processor.ProcessDueAsync();

        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        renewal.State.Should().Be(RenewalState.Failed);
        renewal.LastErrorCode.Should().Be(RenewalErrorCode.CardDeclined);
    }

    [Fact]
    public async Task RequiresAction_RecordsTheAuthenticationRequiredCode()
    {
        _harness.PaymentMethod.Respond = _ => new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.RequiresAction };
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);

        await _harness.Processor.ProcessDueAsync();

        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        renewal.State.Should().Be(RenewalState.PaymentActionRequired);
        renewal.LastErrorCode.Should().Be(RenewalErrorCode.AuthenticationRequired);
    }

    [Fact]
    public async Task ProviderRetry_RecordsTheProviderRetryCode()
    {
        _harness.PaymentMethod.Respond = _ => new ProcessStoredPaymentRequestResult
        {
            Outcome = ChargeOutcome.SoftDecline,
            IsProviderRetryScheduled = true,
        };
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);

        await _harness.Processor.ProcessDueAsync();

        _harness.GetRenewalsOf(subscription.Id).Single().LastErrorCode.Should().Be(RenewalErrorCode.CardDeclinedProviderRetry);
    }

    [Fact]
    public async Task UnavailableItem_AsksForAReviewWithItsCode_AndAcceptingClearsIt()
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
                    copy.Message = "This product is not available right now.";
                    result.Items.Add(copy);
                }

                return result;
            });
        var subscription = _harness.AddSubscription("customer-1", _harness.Now);

        await _harness.Processor.ProcessDueAsync();

        var renewal = _harness.GetRenewalsOf(subscription.Id).Single();
        renewal.State.Should().Be(RenewalState.ReviewRequired);
        renewal.ReviewReasonCode.Should().Be(RenewalReviewReasonCode.ItemsUnavailable);
        renewal.ReviewReason.Should().StartWith("Some items are not available");

        renewal = await _harness.Processor.AcceptReviewAsync(renewal.Id);

        renewal.ReviewReasonCode.Should().BeNull();
        renewal.ReviewReason.Should().BeNull();
    }

    [Fact]
    public void Codes_AreUniqueAndStable()
    {
        RenewalErrorCode.All.Should().OnlyHaveUniqueItems().And.AllSatisfy(x => x.Should().MatchRegex("^[A-Z_]+$"));
        RenewalReviewReasonCode.All.Should().OnlyHaveUniqueItems().And.AllSatisfy(x => x.Should().MatchRegex("^[A-Z_]+$"));
        RenewalReviewReasonCode.All.Should().BeEquivalentTo("ITEMS_UNAVAILABLE", "NO_SHIPPING_METHOD", "PRICE_INCREASED", "CONFIGURATION_CHANGED", "NO_PAYMENT_METHOD");
    }
}
