using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Exceptions;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Xapi.Queries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class AcceptSubscriptionTermsCommandHandler(
    IShoppingCartService cartService,
    ISubscriptionTermsService termsService,
    IPaymentConsentService consentService,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<AcceptSubscriptionTermsCommand, string>
{
    public virtual Task<string> Handle(AcceptSubscriptionTermsCommand request, CancellationToken cancellationToken) => CommandErrors.Run(async () =>
    {
        var cart = await cartService.GetNoCloneAsync(request.CartId) ?? throw new SubscriptionOperationException("Cart not found.");
        AccessGuard.EnsureOwner(request.UserId, cart.CustomerId);

        var terms = await termsService.GetTermsAsync(cart.StoreId);
        if (terms.Version != request.TermsVersion || terms.Hash != request.TermsHash)
        {
            throw new SubscriptionOperationException("The subscription terms have changed. Please review them again.");
        }

        var httpContext = httpContextAccessor.HttpContext;
        var consent = AbstractTypeFactory<PaymentConsent>.TryCreateInstance();
        consent.StoreId = cart.StoreId;
        consent.CustomerId = request.UserId;
        consent.Purpose = ModuleConstants.ConsentPurpose;
        consent.TermsVersion = terms.Version;
        consent.TermsText = terms.Text;
        consent.TermsHash = terms.Hash;
        consent.Channel = "storefront";
        consent.IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        consent.UserAgent = httpContext?.Request.Headers.UserAgent.ToString();
        consent.AcceptedDate = DateTime.UtcNow;
        consent.ObjectType = ModuleConstants.ConsentObjectType;
        consent.ObjectId = GetCheckoutId(cart);

        await consentService.SaveChangesAsync([consent]);
        return consent.Id;
    });

    /// <summary>The checkout the consent is given for (<see cref="ModuleConstants.ConsentObjectType"/>).</summary>
    protected static string GetCheckoutId(VirtoCommerce.CartModule.Core.Model.ShoppingCart cart)
    {
        return string.IsNullOrEmpty(cart.CheckoutId) ? cart.Id : cart.CheckoutId;
    }
}
