using MediatR;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class RemoveCartItemSubscriptionCommand : IRequest<bool>, IHasUserId
{
    public string CartId { get; set; }
    public string LineItemId { get; set; }
    public string UserId { get; set; }
}
