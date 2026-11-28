using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

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
