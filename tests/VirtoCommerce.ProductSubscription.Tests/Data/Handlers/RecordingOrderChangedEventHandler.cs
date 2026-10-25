using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Handlers;
using VirtoCommerce.ProductSubscription.Tests.Data.Renewals;

namespace VirtoCommerce.ProductSubscription.Tests.Data.Handlers;

/// <summary>Records what the handler enqueues instead of using the background job engine.</summary>
public class RecordingOrderChangedEventHandler()
    : OrderChangedEventHandler(Mock.Of<ISubscriptionManager>(), new InProcessDistributedLock(), NullLogger<OrderChangedEventHandler>.Instance)
{
    public List<OrderChangedJobPayload> Enqueued { get; } = [];

    /// <summary>Makes enqueuing fail, as it does when no background job engine is installed.</summary>
    public bool Fail { get; set; }

    protected override Task EnqueueAsync(OrderChangedJobPayload payload)
    {
        if (Fail)
        {
            throw new InvalidOperationException("BackgroundJob static facade is not initialized.");
        }

        Enqueued.Add(payload);
        return Task.CompletedTask;
    }
}
