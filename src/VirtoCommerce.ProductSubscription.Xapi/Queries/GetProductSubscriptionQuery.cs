using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetProductSubscriptionQuery : Query<Subscription>, IHasUserId
{
    public string Id { get; set; }
    public string UserId { get; set; }

    /// <summary>Culture the money fields are formatted in ("en-US").</summary>
    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(Id));
        yield return Argument<StringGraphType>(nameof(CultureName), description: "Culture the money fields are formatted in (\"en-US\")");
    }

    public override void Map(IResolveFieldContext context)
    {
        Id = context.GetArgument<string>(nameof(Id));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
