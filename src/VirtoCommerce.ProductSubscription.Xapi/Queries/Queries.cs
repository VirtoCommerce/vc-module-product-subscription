using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public interface IHasUserId
{
    string UserId { get; set; }
}

internal static class AccessGuard
{
    public static string RequireUser(IResolveFieldContext context)
    {
        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        return context.GetCurrentUserId();
    }

    public static void EnsureOwner(string userId, string ownerId)
    {
        if (string.IsNullOrEmpty(userId) || userId != ownerId)
        {
            throw AuthorizationError.Forbidden();
        }
    }
}

#region productSubscriptions

public class GetProductSubscriptionsQuery : SearchQuery<SubscriptionSearchResult>, IHasUserId
{
    public string StoreId { get; set; }
    public IList<string> Statuses { get; set; }
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
        yield return Argument<ListGraphType<StringGraphType>>(nameof(Statuses));
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Statuses = context.GetArgument<List<string>>(nameof(Statuses));
    }
}

public class GetProductSubscriptionsQueryBuilder(IAuthorizationService authorizationService)
    : SearchQueryBuilder<GetProductSubscriptionsQuery, SubscriptionSearchResult, Subscription, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "productSubscriptions";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetProductSubscriptionsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

public class GetProductSubscriptionsQueryHandler(ISubscriptionSearchService searchService)
    : IQueryHandler<GetProductSubscriptionsQuery, SubscriptionSearchResult>
{
    public virtual Task<SubscriptionSearchResult> Handle(GetProductSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        var criteria = request.GetSearchCriteria<SubscriptionSearchCriteria>();
        criteria.StoreId = request.StoreId;
        criteria.CustomerId = request.UserId;
        criteria.Statuses = request.Statuses?.Where(x => !string.IsNullOrEmpty(x)).ToList();
        return searchService.SearchAsync(criteria);
    }
}

#endregion

#region productSubscription

public class GetProductSubscriptionQuery : Query<Subscription>, IHasUserId
{
    public string Id { get; set; }
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(Id));
    }

    public override void Map(IResolveFieldContext context)
    {
        Id = context.GetArgument<string>(nameof(Id));
    }
}

public class GetProductSubscriptionQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetProductSubscriptionQuery, Subscription, ProductSubscriptionType>(authorizationService)
{
    protected override string Name => "productSubscription";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetProductSubscriptionQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

public class GetProductSubscriptionQueryHandler(ISubscriptionService subscriptionService)
    : IQueryHandler<GetProductSubscriptionQuery, Subscription>
{
    public virtual async Task<Subscription> Handle(GetProductSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.GetByIdAsync(request.Id);
        if (subscription == null)
        {
            return null;
        }

        AccessGuard.EnsureOwner(request.UserId, subscription.CustomerId);
        return subscription;
    }
}

#endregion

#region subscriptionRenewals

public class GetSubscriptionRenewalsQuery : SearchQuery<RenewalSearchResult>, IHasUserId
{
    public string StoreId { get; set; }
    public bool? OnlyOpen { get; set; }
    public string SubscriptionId { get; set; }
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
        yield return Argument<BooleanGraphType>(nameof(OnlyOpen));
        yield return Argument<StringGraphType>(nameof(SubscriptionId));
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);
        StoreId = context.GetArgument<string>(nameof(StoreId));
        OnlyOpen = context.GetArgument<bool?>(nameof(OnlyOpen));
        SubscriptionId = context.GetArgument<string>(nameof(SubscriptionId));
    }
}

public class GetSubscriptionRenewalsQueryBuilder(IAuthorizationService authorizationService)
    : SearchQueryBuilder<GetSubscriptionRenewalsQuery, RenewalSearchResult, Renewal, RenewalType>(authorizationService)
{
    protected override string Name => "subscriptionRenewals";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetSubscriptionRenewalsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

public class GetSubscriptionRenewalsQueryHandler(IRenewalSearchService searchService)
    : IQueryHandler<GetSubscriptionRenewalsQuery, RenewalSearchResult>
{
    public virtual Task<RenewalSearchResult> Handle(GetSubscriptionRenewalsQuery request, CancellationToken cancellationToken)
    {
        var criteria = request.GetSearchCriteria<RenewalSearchCriteria>();
        criteria.StoreId = request.StoreId;
        criteria.CustomerId = request.UserId;
        criteria.OnlyOpen = request.OnlyOpen;
        criteria.SubscriptionId = request.SubscriptionId;
        if (string.IsNullOrEmpty(criteria.Sort))
        {
            criteria.Sort = request.OnlyOpen == true ? "ScheduledDate" : "ScheduledDate:desc";
        }

        return searchService.SearchAsync(criteria);
    }
}

#endregion

#region subscriptionRenewal

public class GetSubscriptionRenewalQuery : Query<Renewal>, IHasUserId
{
    public string Id { get; set; }
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(Id));
    }

    public override void Map(IResolveFieldContext context)
    {
        Id = context.GetArgument<string>(nameof(Id));
    }
}

public class GetSubscriptionRenewalQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSubscriptionRenewalQuery, Renewal, RenewalType>(authorizationService)
{
    protected override string Name => "subscriptionRenewal";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetSubscriptionRenewalQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

public class GetSubscriptionRenewalQueryHandler(IRenewalService renewalService)
    : IQueryHandler<GetSubscriptionRenewalQuery, Renewal>
{
    public virtual async Task<Renewal> Handle(GetSubscriptionRenewalQuery request, CancellationToken cancellationToken)
    {
        var renewal = await renewalService.GetByIdAsync(request.Id);
        if (renewal == null)
        {
            return null;
        }

        AccessGuard.EnsureOwner(request.UserId, renewal.CustomerId);
        return renewal;
    }
}

#endregion

#region savedPaymentMethods

public class GetSavedPaymentMethodsQuery : Query<IList<StoredPaymentMethod>>, IHasUserId
{
    public string StoreId { get; set; }
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
    }
}

public class GetSavedPaymentMethodsQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSavedPaymentMethodsQuery, IList<StoredPaymentMethod>, NonNullGraphType<ListGraphType<NonNullGraphType<SavedPaymentMethodType>>>>(authorizationService)
{
    protected override string Name => "savedPaymentMethods";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetSavedPaymentMethodsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

public class GetSavedPaymentMethodsQueryHandler(IStoredPaymentMethodSearchService searchService)
    : IQueryHandler<GetSavedPaymentMethodsQuery, IList<StoredPaymentMethod>>
{
    public virtual async Task<IList<StoredPaymentMethod>> Handle(GetSavedPaymentMethodsQuery request, CancellationToken cancellationToken)
    {
        var criteria = AbstractTypeFactory<StoredPaymentMethodSearchCriteria>.TryCreateInstance();
        criteria.StoreId = request.StoreId;
        criteria.CustomerId = request.UserId;
        criteria.Statuses = [StoredPaymentMethodStatus.Active, StoredPaymentMethodStatus.NeedsUpdate, StoredPaymentMethodStatus.Expired];
        criteria.Take = 100;
        return (await searchService.SearchAsync(criteria)).Results;
    }
}

#endregion

#region subscriptionTerms

public class GetSubscriptionTermsQuery : Query<SubscriptionTerms>
{
    public string StoreId { get; set; }
    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
        yield return Argument<StringGraphType>(nameof(CultureName));
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}

public class GetSubscriptionTermsQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSubscriptionTermsQuery, SubscriptionTerms, SubscriptionTermsType>(authorizationService)
{
    protected override string Name => "subscriptionTerms";
}

public class GetSubscriptionTermsQueryHandler(ISubscriptionTermsService termsService)
    : IQueryHandler<GetSubscriptionTermsQuery, SubscriptionTerms>
{
    public virtual Task<SubscriptionTerms> Handle(GetSubscriptionTermsQuery request, CancellationToken cancellationToken)
    {
        return termsService.GetTermsAsync(request.StoreId, request.CultureName);
    }
}

#endregion

#region subscriptionOffers

public class GetSubscriptionOffersQuery : Query<IList<SubscriptionOffer>>
{
    public string StoreId { get; set; }
    public IList<string> ProductIds { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
        yield return Argument<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(nameof(ProductIds));
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        ProductIds = context.GetArgument<List<string>>(nameof(ProductIds));
    }
}

public class GetSubscriptionOffersQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetSubscriptionOffersQuery, IList<SubscriptionOffer>, NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionOfferType>>>>(authorizationService)
{
    protected override string Name => "subscriptionOffers";
}

public class GetSubscriptionOffersQueryHandler(ISubscriptionOfferService offerService)
    : IQueryHandler<GetSubscriptionOffersQuery, IList<SubscriptionOffer>>
{
    public virtual async Task<IList<SubscriptionOffer>> Handle(GetSubscriptionOffersQuery request, CancellationToken cancellationToken)
    {
        var offers = await offerService.GetOffersAsync(request.StoreId, request.ProductIds?.Take(100).ToList());
        return offers.Values.ToList();
    }
}

#endregion

#region cartSubscriptionItems

public class GetCartSubscriptionItemsQuery : Query<IList<SubscriptionCartItem>>, IHasUserId
{
    public string CartId { get; set; }
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(CartId));
    }

    public override void Map(IResolveFieldContext context)
    {
        CartId = context.GetArgument<string>(nameof(CartId));
    }
}

public class GetCartSubscriptionItemsQueryBuilder(IAuthorizationService authorizationService)
    : QueryBuilder<GetCartSubscriptionItemsQuery, IList<SubscriptionCartItem>, NonNullGraphType<ListGraphType<NonNullGraphType<SubscriptionCartItemType>>>>(authorizationService)
{
    protected override string Name => "cartSubscriptionItems";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, GetCartSubscriptionItemsQuery request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

public class GetCartSubscriptionItemsQueryHandler(IShoppingCartService cartService, ISubscriptionCartItemSearchService searchService)
    : IQueryHandler<GetCartSubscriptionItemsQuery, IList<SubscriptionCartItem>>
{
    public virtual async Task<IList<SubscriptionCartItem>> Handle(GetCartSubscriptionItemsQuery request, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetNoCloneAsync(request.CartId);
        if (cart == null)
        {
            return [];
        }

        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var criteria = AbstractTypeFactory<SubscriptionCartItemSearchCriteria>.TryCreateInstance();
        criteria.CartIds = [cart.Id];
        criteria.Take = 1000;
        var lineIds = cart.Items?.Select(x => x.Id).ToHashSet() ?? [];
        return (await searchService.SearchAsync(criteria)).Results.Where(x => lineIds.Contains(x.LineItemId)).ToList();
    }
}

#endregion
