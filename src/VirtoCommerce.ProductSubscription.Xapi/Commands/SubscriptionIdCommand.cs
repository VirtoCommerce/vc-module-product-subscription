using MediatR;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SubscriptionIdCommand : IRequest<Subscription>, IHasUserId
{
    public string SubscriptionId { get; set; }
    public string UserId { get; set; }
}
