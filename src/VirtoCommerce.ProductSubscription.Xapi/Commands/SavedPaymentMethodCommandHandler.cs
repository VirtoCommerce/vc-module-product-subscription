using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model.Search;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

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
            throw new InvalidOperationException(usedBy == 1
                ? "This card pays for a subscription. Choose another card for it first."
                : $"This card pays for {usedBy} subscriptions. Choose another card for them first.");
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
