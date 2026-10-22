using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.Search;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

/// <summary>Base for every subscription mutation: requires a signed-in user and passes the user id to the handler.</summary>
public abstract class SubscriptionCommandBuilder<TCommand, TResult, TInputGraphType, TResultGraphType>(IAuthorizationService authorizationService)
    : CommandBuilder<TCommand, TResult, TInputGraphType, TResultGraphType>(authorizationService)
    where TCommand : IRequest<TResult>, IHasUserId
    where TInputGraphType : IInputObjectGraphType
    where TResultGraphType : IGraphType
{
    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TCommand request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

internal static class CommandErrors
{
    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            throw new ExecutionError(ex.Message) { Code = "SUBSCRIPTION_ERROR" };
        }
    }
}

#region Cart

public class SetCartItemSubscriptionCommand : IRequest<SubscriptionCartItem>, IHasUserId
{
    public string StoreId { get; set; }
    public string CartId { get; set; }
    public string LineItemId { get; set; }
    public string PlanId { get; set; }
    public string IntervalUnit { get; set; }
    public int IntervalCount { get; set; }
    public string UserId { get; set; }
}

public class SetCartItemSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<SetCartItemSubscriptionCommand, SubscriptionCartItem, InputSetCartItemSubscriptionType, SubscriptionCartItemType>(authorizationService)
{
    protected override string Name => "setCartItemSubscription";
}

public class SetCartItemSubscriptionCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionOfferService offerService,
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionCartItemService cartItemService)
    : IRequestHandler<SetCartItemSubscriptionCommand, SubscriptionCartItem>
{
    public virtual Task<SubscriptionCartItem> Handle(SetCartItemSubscriptionCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var cart = await cartService.GetByIdAsync(request.CartId) ?? throw new InvalidOperationException("Cart not found.");
        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var lineItem = cart.Items?.FirstOrDefault(x => x.Id == request.LineItemId) ?? throw new InvalidOperationException("Cart item not found.");
        var unit = EnumUtility.SafeParse(request.IntervalUnit, SubscriptionIntervalUnit.Month);
        var count = Math.Max(1, request.IntervalCount);

        var offers = await offerService.GetOffersAsync(cart.StoreId, [lineItem.ProductId]);
        if (!offers.TryGetValue(lineItem.ProductId, out var offer) || offer.PlanId != request.PlanId)
        {
            throw new InvalidOperationException("This product can't be subscribed to.");
        }

        if (!offer.Intervals.Any(x => x.Unit == unit && x.Count == count))
        {
            throw new InvalidOperationException("This frequency is not offered for this product.");
        }

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.LineItemId = lineItem.Id;
        criteria.Take = 1;
        var item = (await cartItemSearchService.SearchAsync(criteria)).Results.FirstOrDefault()
            ?? AbstractTypeFactory<SubscriptionCartItem>.TryCreateInstance();

        item.StoreId = cart.StoreId;
        item.CartId = cart.Id;
        item.LineItemId = lineItem.Id;
        item.ProductId = lineItem.ProductId;
        item.CustomerId = cart.CustomerId;
        item.PlanId = offer.PlanId;
        item.IntervalUnit = unit;
        item.IntervalCount = count;

        await cartItemService.SaveChangesAsync([item]);
        await TouchCartAsync(cart);

        return item;
    });

    /// <summary>Saving the cart expires xCart's cached aggregate, so the next cart query shows the subscription discount.</summary>
    protected virtual Task TouchCartAsync(ShoppingCart cart) => cartService.SaveChangesAsync([cart]);
}

public class RemoveCartItemSubscriptionCommand : IRequest<bool>, IHasUserId
{
    public string CartId { get; set; }
    public string LineItemId { get; set; }
    public string UserId { get; set; }
}

public class RemoveCartItemSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<RemoveCartItemSubscriptionCommand, bool, InputRemoveCartItemSubscriptionType, BooleanGraphType>(authorizationService)
{
    protected override string Name => "removeCartItemSubscription";
}

public class RemoveCartItemSubscriptionCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionCartItemSearchService cartItemSearchService,
    ISubscriptionCartItemService cartItemService)
    : IRequestHandler<RemoveCartItemSubscriptionCommand, bool>
{
    public virtual async Task<bool> Handle(RemoveCartItemSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetByIdAsync(request.CartId);
        if (cart == null)
        {
            return false;
        }

        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.LineItemId = request.LineItemId;
        criteria.Take = 10;
        var items = (await cartItemSearchService.SearchAsync(criteria)).Results;
        if (items.Count == 0)
        {
            return false;
        }

        await cartItemService.DeleteAsync(items.Select(x => x.Id).ToList());
        await cartService.SaveChangesAsync([cart]);
        return true;
    }
}

public class AcceptSubscriptionTermsCommand : IRequest<string>, IHasUserId
{
    public string StoreId { get; set; }
    public string CartId { get; set; }
    public string TermsVersion { get; set; }
    public string TermsHash { get; set; }
    public string UserId { get; set; }
}

public class AcceptSubscriptionTermsCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<AcceptSubscriptionTermsCommand, string, InputAcceptSubscriptionTermsType, StringGraphType>(authorizationService)
{
    protected override string Name => "acceptSubscriptionTerms";
}

public class AcceptSubscriptionTermsCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionTermsService termsService,
    IPaymentConsentService consentService,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<AcceptSubscriptionTermsCommand, string>
{
    public virtual Task<string> Handle(AcceptSubscriptionTermsCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var cart = await cartService.GetNoCloneAsync(request.CartId) ?? throw new InvalidOperationException("Cart not found.");
        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var terms = await termsService.GetTermsAsync(cart.StoreId);
        if (terms.Version != request.TermsVersion || terms.Hash != request.TermsHash)
        {
            throw new InvalidOperationException("The subscription terms have changed. Please review them again.");
        }

        var httpContext = httpContextAccessor.HttpContext;
        var consent = AbstractTypeFactory<PaymentConsent>.TryCreateInstance();
        consent.StoreId = cart.StoreId;
        consent.CustomerId = request.UserId;
        consent.Purpose = ModuleConstants.ConsentPurpose;
        consent.TermsVersion = terms.Version;
        consent.TermsText = terms.Text;
        consent.TermsHash = terms.Hash;
        consent.Channel = "storefront";
        consent.IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        consent.UserAgent = httpContext?.Request.Headers.UserAgent.ToString();
        consent.AcceptedDate = DateTime.UtcNow;
        consent.ObjectType = SubscriptionManager.CartObjectType;
        consent.ObjectId = cart.Id;

        await consentService.SaveChangesAsync([consent]);
        return consent.Id;
    });
}

#endregion

#region Subscription actions

public class SubscriptionIdCommand : IRequest<Subscription>, IHasUserId
{
    public string SubscriptionId { get; set; }
    public string UserId { get; set; }
}

public class SkipProductSubscriptionCommand : SubscriptionIdCommand;

public class ResumeProductSubscriptionCommand : SubscriptionIdCommand;

public class PauseProductSubscriptionCommand : SubscriptionIdCommand
{
    public DateTime? ResumeDate { get; set; }
}

public class CancelProductSubscriptionCommand : SubscriptionIdCommand
{
    public string Reason { get; set; }
}

public class ChangeProductSubscriptionCommand : SubscriptionIdCommand
{
    public string IntervalUnit { get; set; }
    public int? IntervalCount { get; set; }
    public int? Quantity { get; set; }
    public DateTime? NextOrderDate { get; set; }
    public string SavedPaymentMethodId { get; set; }
}

public class SkipProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<SkipProductSubscriptionCommand, Subscription, InputSubscriptionIdType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "skipProductSubscription";
}

public class ResumeProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<ResumeProductSubscriptionCommand, Subscription, InputSubscriptionIdType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "resumeProductSubscription";
}

public class PauseProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<PauseProductSubscriptionCommand, Subscription, InputPauseSubscriptionType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "pauseProductSubscription";
}

public class CancelProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<CancelProductSubscriptionCommand, Subscription, InputCancelSubscriptionType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "cancelProductSubscription";
}

public class ChangeProductSubscriptionCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<ChangeProductSubscriptionCommand, Subscription, InputChangeSubscriptionType, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "changeProductSubscription";
}

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
            IntervalUnit = string.IsNullOrEmpty(request.IntervalUnit) ? null : EnumUtility.SafeParse(request.IntervalUnit, SubscriptionIntervalUnit.Month),
            IntervalCount = request.IntervalCount,
            Quantity = request.Quantity,
            NextOrderDate = request.NextOrderDate,
            StoredPaymentMethodId = request.SavedPaymentMethodId,
        }));

    protected virtual Task<Subscription> Owned(SubscriptionIdCommand request, Func<Task<Subscription>> action) => CommandErrors.Run(async () =>
    {
        var subscription = await subscriptionService.GetNoCloneAsync(request.SubscriptionId) ?? throw new InvalidOperationException("Subscription not found.");
        AccessGuard.EnsureOwner(request.UserId, subscription.CustomerId);
        return await action();
    });
}

#endregion

#region Renewal actions

public class OrderProductSubscriptionNowCommand : IRequest<Renewal>, IHasUserId
{
    public string SubscriptionId { get; set; }
    public string UserId { get; set; }
}

public class OrderProductSubscriptionNowCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<OrderProductSubscriptionNowCommand, Renewal, InputSubscriptionIdType, RenewalType>(authorizationService)
{
    protected override string Name => "orderProductSubscriptionNow";
}

public class RenewalIdCommand : IRequest<Renewal>, IHasUserId
{
    public string RenewalId { get; set; }
    public string UserId { get; set; }
}

public class AcceptSubscriptionRenewalCommand : RenewalIdCommand;

public class SkipSubscriptionRenewalCommand : RenewalIdCommand;

public class RetrySubscriptionRenewalPaymentCommand : RenewalIdCommand;

public class AcceptSubscriptionRenewalCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<AcceptSubscriptionRenewalCommand, Renewal, InputRenewalIdType, RenewalType>(authorizationService)
{
    protected override string Name => "acceptSubscriptionRenewal";
}

public class SkipSubscriptionRenewalCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<SkipSubscriptionRenewalCommand, Renewal, InputRenewalIdType, RenewalType>(authorizationService)
{
    protected override string Name => "skipSubscriptionRenewal";
}

public class RetrySubscriptionRenewalPaymentCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<RetrySubscriptionRenewalPaymentCommand, Renewal, InputRenewalIdType, RenewalType>(authorizationService)
{
    protected override string Name => "retrySubscriptionRenewalPayment";
}

public class RenewalCommandHandler(ISubscriptionService subscriptionService, IRenewalService renewalService, IRenewalProcessor renewalProcessor)
    : IRequestHandler<OrderProductSubscriptionNowCommand, Renewal>,
        IRequestHandler<AcceptSubscriptionRenewalCommand, Renewal>,
        IRequestHandler<SkipSubscriptionRenewalCommand, Renewal>,
        IRequestHandler<RetrySubscriptionRenewalPaymentCommand, Renewal>
{
    public virtual Task<Renewal> Handle(OrderProductSubscriptionNowCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var subscription = await subscriptionService.GetNoCloneAsync(request.SubscriptionId) ?? throw new InvalidOperationException("Subscription not found.");
        AccessGuard.EnsureOwner(request.UserId, subscription.CustomerId);
        return await renewalProcessor.OrderNowAsync(subscription.Id);
    });

    public virtual Task<Renewal> Handle(AcceptSubscriptionRenewalCommand request, CancellationToken cancellationToken)
        => Owned(request, () => renewalProcessor.AcceptReviewAsync(request.RenewalId));

    public virtual Task<Renewal> Handle(SkipSubscriptionRenewalCommand request, CancellationToken cancellationToken)
        => Owned(request, () => renewalProcessor.SkipAsync(request.RenewalId));

    public virtual Task<Renewal> Handle(RetrySubscriptionRenewalPaymentCommand request, CancellationToken cancellationToken)
        => Owned(request, () => renewalProcessor.RetryPaymentAsync(request.RenewalId));

    protected virtual Task<Renewal> Owned(RenewalIdCommand request, Func<Task<Renewal>> action) => CommandErrors.Run(async () =>
    {
        var renewal = await renewalService.GetNoCloneAsync(request.RenewalId) ?? throw new InvalidOperationException("Renewal not found.");
        AccessGuard.EnsureOwner(request.UserId, renewal.CustomerId);
        return await action();
    });
}

#endregion

#region Saved payment methods

public class AddSavedPaymentMethodCommand : IRequest<StoredPaymentMethod>, IHasUserId
{
    public string StoreId { get; set; }
    public string PaymentMethodCode { get; set; }
    public IList<KeyValue> Parameters { get; set; }
    public bool? SetDefault { get; set; }
    public string UserId { get; set; }
}

public class AddSavedPaymentMethodCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<AddSavedPaymentMethodCommand, StoredPaymentMethod, InputAddSavedPaymentMethodType, SavedPaymentMethodType>(authorizationService)
{
    protected override string Name => "addSavedPaymentMethod";
}

public class SavedPaymentMethodIdCommand : IRequest<StoredPaymentMethod>, IHasUserId
{
    public string Id { get; set; }
    public string UserId { get; set; }
}

public class RemoveSavedPaymentMethodCommand : SavedPaymentMethodIdCommand;

public class SetDefaultSavedPaymentMethodCommand : SavedPaymentMethodIdCommand;

public class RemoveSavedPaymentMethodCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<RemoveSavedPaymentMethodCommand, StoredPaymentMethod, InputSavedPaymentMethodIdType, SavedPaymentMethodType>(authorizationService)
{
    protected override string Name => "removeSavedPaymentMethod";
}

public class SetDefaultSavedPaymentMethodCommandBuilder(IAuthorizationService authorizationService)
    : SubscriptionCommandBuilder<SetDefaultSavedPaymentMethodCommand, StoredPaymentMethod, InputSavedPaymentMethodIdType, SavedPaymentMethodType>(authorizationService)
{
    protected override string Name => "setDefaultSavedPaymentMethod";
}

public class SavedPaymentMethodCommandHandler(
    IStoredPaymentMethodService storedPaymentMethodService,
    IStoredPaymentMethodSearchService storedPaymentMethodSearchService,
    IPaymentMethodsSearchService paymentMethodsSearchService,
    ISubscriptionSearchService subscriptionSearchService)
    : IRequestHandler<AddSavedPaymentMethodCommand, StoredPaymentMethod>,
        IRequestHandler<RemoveSavedPaymentMethodCommand, StoredPaymentMethod>,
        IRequestHandler<SetDefaultSavedPaymentMethodCommand, StoredPaymentMethod>
{
    public virtual Task<StoredPaymentMethod> Handle(AddSavedPaymentMethodCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var criteria = AbstractTypeFactory<PaymentMethodsSearchCriteria>.TryCreateInstance();
        criteria.StoreId = request.StoreId;
        criteria.Codes = [request.PaymentMethodCode];
        criteria.IsActive = true;
        criteria.Take = 1;
        var paymentMethod = (await paymentMethodsSearchService.SearchAsync(criteria)).Results.FirstOrDefault();
        if (paymentMethod is not ISupportStoredCredentialFlow)
        {
            throw new InvalidOperationException("This payment method can't save cards.");
        }

        var parameters = new NameValueCollection();
        foreach (var parameter in request.Parameters ?? [])
        {
            parameters[parameter.Key] = parameter.Value;
        }

        var result = await paymentMethod.StorePaymentMethodAsync(new StorePaymentMethodRequest
        {
            StoreId = request.StoreId,
            CustomerId = request.UserId,
            Usage = StoredCredentialUsage.Recurring,
            Verify = true,
            Parameters = parameters,
        }, cancellationToken);

        if (result.Outcome == ChargeOutcome.RequiresAction)
        {
            throw new ExecutionError(result.ErrorMessage ?? "Your bank needs you to confirm this card.") { Code = "AUTHENTICATION_REQUIRED" };
        }

        if (!result.IsSuccess || result.StoredPaymentMethod == null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "The card could not be saved.");
        }

        var existing = await GetCustomerMethodsAsync(request.UserId, request.StoreId);
        var method = result.StoredPaymentMethod;
        method.StoreId = request.StoreId;
        method.CustomerId = request.UserId;
        method.IsDefault = request.SetDefault == true || existing.Count == 0;

        var toSave = new List<StoredPaymentMethod> { method };
        if (method.IsDefault)
        {
            foreach (var other in existing.Where(x => x.IsDefault))
            {
                other.IsDefault = false;
                toSave.Add(other);
            }
        }

        await storedPaymentMethodService.SaveChangesAsync(toSave);
        return method;
    });

    public virtual Task<StoredPaymentMethod> Handle(RemoveSavedPaymentMethodCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var method = await GetOwnedAsync(request);

        var usage = AbstractTypeFactory<SubscriptionSearchCriteria>.TryCreateInstance();
        usage.StoredPaymentMethodId = method.Id;
        usage.Statuses = [SubscriptionStatus.Active, SubscriptionStatus.Paused, SubscriptionStatus.PastDue, SubscriptionStatus.PendingActivation];
        usage.Take = 0;
        var usedBy = (await subscriptionSearchService.SearchAsync(usage)).TotalCount;
        if (usedBy > 0)
        {
            throw new InvalidOperationException($"This card pays for {usedBy} subscription(s). Choose another card for them first.");
        }

        var criteria = AbstractTypeFactory<PaymentMethodsSearchCriteria>.TryCreateInstance();
        criteria.StoreId = method.StoreId;
        criteria.Codes = [method.PaymentMethodCode];
        criteria.Take = 1;
        var paymentMethod = (await paymentMethodsSearchService.SearchAsync(criteria)).Results.FirstOrDefault();
        if (paymentMethod is ISupportStoredCredentialFlow)
        {
            await paymentMethod.DeleteStoredPaymentMethodAsync(new DeleteStoredPaymentMethodRequest { StoreId = method.StoreId, StoredPaymentMethod = method }, cancellationToken);
        }

        method.Status = StoredPaymentMethodStatus.Revoked;
        method.IsDefault = false;
        await storedPaymentMethodService.SaveChangesAsync([method]);
        return method;
    });

    public virtual Task<StoredPaymentMethod> Handle(SetDefaultSavedPaymentMethodCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var method = await GetOwnedAsync(request);
        if (method.Status != StoredPaymentMethodStatus.Active)
        {
            throw new InvalidOperationException("Only an active card can be the default.");
        }

        var existing = await GetCustomerMethodsAsync(method.CustomerId, method.StoreId);
        var toSave = existing.Where(x => x.IsDefault && x.Id != method.Id).ToList();
        foreach (var other in toSave)
        {
            other.IsDefault = false;
        }

        method.IsDefault = true;
        toSave.Add(method);
        await storedPaymentMethodService.SaveChangesAsync(toSave);
        return method;
    });

    protected virtual async Task<StoredPaymentMethod> GetOwnedAsync(SavedPaymentMethodIdCommand request)
    {
        var method = await storedPaymentMethodService.GetByIdAsync(request.Id) ?? throw new InvalidOperationException("Saved card not found.");
        AccessGuard.EnsureOwner(request.UserId, method.CustomerId);
        return method;
    }

    protected virtual async Task<IList<StoredPaymentMethod>> GetCustomerMethodsAsync(string customerId, string storeId)
    {
        var criteria = AbstractTypeFactory<StoredPaymentMethodSearchCriteria>.TryCreateInstance();
        criteria.CustomerId = customerId;
        criteria.StoreId = storeId;
        criteria.Statuses = [StoredPaymentMethodStatus.Active];
        criteria.Take = 100;
        return (await storedPaymentMethodSearchService.SearchAsync(criteria)).Results;
    }
}

#endregion
