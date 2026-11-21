using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

public class GetSubscriptionOffersQuery : Query<IList<SubscriptionOffer>>
{
    public string StoreId { get; set; }
    public IList<string> ProductIds { get; set; }

    /// <summary>Currency of the offer prices; the store's default currency when empty.</summary>
    public string CurrencyCode { get; set; }

    /// <summary>Culture the money fields are formatted in ("en-US").</summary>
    public string CultureName { get; set; }

    /// <summary>The customer the prices are evaluated for (price lists, promotions).</summary>
    public string UserId { get; set; }

    public string OrganizationId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
        yield return Argument<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(nameof(ProductIds));
        yield return Argument<StringGraphType>(nameof(CurrencyCode), description: "Currency of the offer prices (\"USD\"); the store's default currency when empty");
        yield return Argument<StringGraphType>(nameof(CultureName), description: "Culture the money fields are formatted in (\"en-US\")");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        ProductIds = context.GetArgument<List<string>>(nameof(ProductIds));
        CurrencyCode = context.GetArgument<string>(nameof(CurrencyCode));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
        OrganizationId = context.GetCurrentOrganizationId();
    }
}
