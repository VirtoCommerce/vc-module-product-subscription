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

public class RenewalCommandHandler(
    ISubscriptionService subscriptionService,
    IRenewalService renewalService,
    IRenewalProcessor renewalProcessor,
    ISubscriptionCustomerActionsService customerActionsService)
    : IRequestHandler<OrderProductSubscriptionNowCommand, Renewal>,
        IRequestHandler<AcceptSubscriptionRenewalCommand, Renewal>,
        IRequestHandler<SkipSubscriptionRenewalCommand, Renewal>,
        IRequestHandler<RetrySubscriptionRenewalPaymentCommand, Renewal>
{
    public virtual Task<Renewal> Handle(OrderProductSubscriptionNowCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var subscription = await subscriptionService.GetNoCloneAsync(request.SubscriptionId) ?? throw new SubscriptionOperationException("Subscription not found.");
        AccessGuard.EnsureOwner(request.UserId, subscription.CustomerId);
        await EnsureAllowedAsync(subscription.StoreId, actions => actions.CanOrderNow);
        return await renewalProcessor.OrderNowAsync(subscription.Id);
    });

    public virtual Task<Renewal> Handle(AcceptSubscriptionRenewalCommand request, CancellationToken cancellationToken)
        => Owned(request, () => renewalProcessor.AcceptReviewAsync(request.RenewalId));

    public virtual Task<Renewal> Handle(SkipSubscriptionRenewalCommand request, CancellationToken cancellationToken)
        => Owned(request, () => renewalProcessor.SkipAsync(request.RenewalId), actions => actions.CanSkip);

    public virtual Task<Renewal> Handle(RetrySubscriptionRenewalPaymentCommand request, CancellationToken cancellationToken)
        => Owned(request, () => renewalProcessor.RetryPaymentAsync(request.RenewalId));

    /// <summary>Checks that the customer owns the renewal and, when given, that the store lets customers take this action.</summary>
    protected virtual Task<Renewal> Owned(RenewalIdCommand request, Func<Task<Renewal>> action, Func<SubscriptionCustomerActions, bool> isAllowed = null) => CommandErrors.Run(async () =>
    {
        var renewal = await renewalService.GetNoCloneAsync(request.RenewalId) ?? throw new SubscriptionOperationException("Renewal not found.");
        AccessGuard.EnsureOwner(request.UserId, renewal.CustomerId);
        if (isAllowed != null)
        {
            await EnsureAllowedAsync(renewal.StoreId, isAllowed);
        }

        return await action();
    });

    protected virtual async Task EnsureAllowedAsync(string storeId, Func<SubscriptionCustomerActions, bool> isAllowed)
    {
        if (!isAllowed(await customerActionsService.GetAsync(storeId)))
        {
            throw new SubscriptionActionDisabledException();
        }
    }
}
