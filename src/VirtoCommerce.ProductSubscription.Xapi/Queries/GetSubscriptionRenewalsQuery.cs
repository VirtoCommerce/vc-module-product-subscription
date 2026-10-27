using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

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
