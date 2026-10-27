using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class ProductSubscriptionCommandHandler(ISubscriptionService subscriptionService, ISubscriptionManager subscriptionManager)
    : IRequestHandler<SkipProductSubscriptionCommand, Subscription>,
        IRequestHandler<ResumeProductSubscriptionCommand, Subscription>,
        IRequestHandler<PauseProductSubscriptionCommand, Subscription>,
        IRequestHandler<CancelProductSubscriptionCommand, Subscription>,
        IRequestHandler<ChangeProductSubscriptionCommand, Subscription>
{
    public virtual Task<Subscription> Handle(SkipProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, () => subscriptionManager.SkipNextAsync(request.SubscriptionId));

    public virtual Task<Subscription> Handle(ResumeProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, () => subscriptionManager.ResumeAsync(request.SubscriptionId));

    public virtual Task<Subscription> Handle(PauseProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, () => subscriptionManager.PauseAsync(request.SubscriptionId, request.ResumeDate));

    public virtual Task<Subscription> Handle(CancelProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, () => subscriptionManager.CancelAsync(request.SubscriptionId, request.Reason));

    public virtual Task<Subscription> Handle(ChangeProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, () => subscriptionManager.ChangeAsync(request.SubscriptionId, new SubscriptionChange
        {
            IntervalUnit = request.IntervalUnit,
            IntervalCount = request.IntervalCount,
            Quantity = request.Quantity,
            NextOrderDate = request.NextOrderDate,
            StoredPaymentMethodId = request.SavedPaymentMethodId,
        }));

    protected virtual Task<Subscription> Owned(SubscriptionIdCommand request, Func<Task<Subscription>> action) => CommandErrors.Run(async () =>
    {
        var subscription = await subscriptionService.GetNoCloneAsync(request.SubscriptionId) ?? throw new SubscriptionOperationException("Subscription not found.");
        AccessGuard.EnsureOwner(request.UserId, subscription.CustomerId);
        return await action();
    });
}
