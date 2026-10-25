using MediatR;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class RenewalIdCommand : IRequest<Renewal>, IHasUserId
{
    public string RenewalId { get; set; }
    public string UserId { get; set; }
}
