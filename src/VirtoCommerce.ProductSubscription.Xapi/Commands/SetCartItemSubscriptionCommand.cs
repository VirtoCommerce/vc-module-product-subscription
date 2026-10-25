using MediatR;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SetCartItemSubscriptionCommand : IRequest<SubscriptionCartItem>, IHasUserId
{
    public string StoreId { get; set; }
    public string CartId { get; set; }
    public string LineItemId { get; set; }
    public string PlanId { get; set; }
    public string IntervalUnit { get; set; }
    public int IntervalCount { get; set; }
    public string UserId { get; set; }
}
