using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

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
