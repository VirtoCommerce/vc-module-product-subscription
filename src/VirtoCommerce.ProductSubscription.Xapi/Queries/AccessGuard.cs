using GraphQL;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.ProductSubscription.Xapi.Queries;

internal static class AccessGuard
{
    public static string RequireUser(IResolveFieldContext context)
    {
        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        return context.GetCurrentUserId();
    }

    public static void EnsureOwner(string userId, string ownerId)
    {
        if (string.IsNullOrEmpty(userId) || userId != ownerId)
        {
            throw AuthorizationError.Forbidden();
        }
    }
}
