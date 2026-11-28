using MediatR;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class AcceptSubscriptionTermsCommand : IRequest<string>, IHasUserId
{
    public string StoreId { get; set; }
    public string CartId { get; set; }
    public string TermsVersion { get; set; }
    public string TermsHash { get; set; }
    public string UserId { get; set; }
}
