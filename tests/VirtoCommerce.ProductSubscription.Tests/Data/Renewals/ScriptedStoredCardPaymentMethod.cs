using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.ProductSubscription.Data.Payments;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

/// <summary>A stored-card connector whose charge result each test scripts, and which records every charge request.</summary>
public class ScriptedStoredCardPaymentMethod()
    : MockStoredCardPaymentMethod(Mock.Of<IStoredPaymentMethodService>(), Mock.Of<IStoredPaymentMethodSearchService>())
{
    public Func<ProcessStoredPaymentRequest, ProcessStoredPaymentRequestResult> Respond { get; set; } =
        _ => new ProcessStoredPaymentRequestResult { Outcome = ChargeOutcome.Succeeded, TransactionId = Guid.NewGuid().ToString("N") };

    public List<ProcessStoredPaymentRequest> Requests { get; } = [];

    public override Task<ProcessStoredPaymentRequestResult> ProcessStoredPaymentAsync(ProcessStoredPaymentRequest request, CancellationToken cancellationToken = default)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }

        return Task.FromResult(Respond(request));
    }
}
