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

public class ProductSubscriptionCommandHandler(
    ISubscriptionService subscriptionService,
    ISubscriptionManager subscriptionManager,
    ISubscriptionCustomerActionsService customerActionsService)
    : IRequestHandler<SkipProductSubscriptionCommand, Subscription>,
        IRequestHandler<ResumeProductSubscriptionCommand, Subscription>,
        IRequestHandler<PauseProductSubscriptionCommand, Subscription>,
        IRequestHandler<CancelProductSubscriptionCommand, Subscription>,
        IRequestHandler<ChangeProductSubscriptionCommand, Subscription>
{
    public virtual Task<Subscription> Handle(SkipProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, actions => actions.CanSkip, () => subscriptionManager.SkipNextAsync(request.SubscriptionId, request.ExpectedNextOrderDate));

    public virtual Task<Subscription> Handle(ResumeProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, () => subscriptionManager.ResumeAsync(request.SubscriptionId));

    public virtual Task<Subscription> Handle(PauseProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, actions => actions.CanPause, () => subscriptionManager.PauseAsync(request.SubscriptionId, request.ResumeDate));

    public virtual Task<Subscription> Handle(CancelProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, () => subscriptionManager.CancelAsync(request.SubscriptionId, request.Reason));

    public virtual Task<Subscription> Handle(ChangeProductSubscriptionCommand request, CancellationToken cancellationToken)
        => Owned(request, (actions, subscription) => IsChangeAllowed(request, subscription, actions), () => subscriptionManager.ChangeAsync(request.SubscriptionId, new SubscriptionChange
        {
            IntervalUnit = request.IntervalUnit,
            IntervalCount = request.IntervalCount,
            Quantity = request.Quantity,
            NextOrderDate = request.NextOrderDate,
            StoredPaymentMethodId = request.SavedPaymentMethodId,
        }));

    /// <summary>
    /// A frequency or card change needs its store flag; a quantity or date change needs none. A value equal to the current
    /// one isn't a change, so a form that sends every field back still works with the flag off.
    /// </summary>
    protected virtual bool IsChangeAllowed(ChangeProductSubscriptionCommand request, Subscription subscription, SubscriptionCustomerActions actions)
    {
        var changesFrequency = (request.IntervalUnit.HasValue && request.IntervalUnit != subscription.IntervalUnit)
                               || (request.IntervalCount.HasValue && request.IntervalCount != subscription.IntervalCount);
        var changesCard = !string.IsNullOrEmpty(request.SavedPaymentMethodId) && request.SavedPaymentMethodId != subscription.StoredPaymentMethodId;

        return (!changesFrequency || actions.CanChangeFrequency) && (!changesCard || actions.CanChangePaymentMethod);
    }

    protected virtual Task<Subscription> Owned(SubscriptionIdCommand request, Func<Task<Subscription>> action)
        => Owned(request, (_, _) => true, action);

    protected virtual Task<Subscription> Owned(SubscriptionIdCommand request, Func<SubscriptionCustomerActions, bool> isAllowed, Func<Task<Subscription>> action)
        => Owned(request, (actions, _) => isAllowed(actions), action);

    /// <summary>Checks that the customer owns the subscription and that the store lets customers take this action.</summary>
    protected virtual Task<Subscription> Owned(
        SubscriptionIdCommand request,
        Func<SubscriptionCustomerActions, Subscription, bool> isAllowed,
        Func<Task<Subscription>> action) => CommandErrors.Run(async () =>
    {
        var subscription = await subscriptionService.GetNoCloneAsync(request.SubscriptionId) ?? throw new SubscriptionOperationException("Subscription not found.");
        AccessGuard.EnsureOwner(request.UserId, subscription.CustomerId);

        var actions = await customerActionsService.GetAsync(subscription.StoreId);
        if (!isAllowed(actions, subscription))
        {
            throw new SubscriptionActionDisabledException();
        }

        return await action();
    });
}
