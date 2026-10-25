using MediatR;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class SavedPaymentMethodIdCommand : IRequest<StoredPaymentMethod>, IHasUserId
{
    public string Id { get; set; }
    public string UserId { get; set; }
}
