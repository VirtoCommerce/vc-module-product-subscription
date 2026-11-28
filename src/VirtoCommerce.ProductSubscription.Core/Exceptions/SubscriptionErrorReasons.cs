namespace VirtoCommerce.ProductSubscription.Core.Exceptions;

/// <summary>
/// Codes of the reasons a subscription action is refused, sent with the error so a storefront can say it in the
/// customer's language. A refusal without a code shows the server's message.
/// </summary>
public static class SubscriptionErrorReasons
{
    /// <summary>The product has another line in the cart that isn't subscribed (another configuration of it).</summary>
    public const string ProductInCartMoreThanOnce = "PRODUCT_IN_CART_MORE_THAN_ONCE";

    /// <summary>The product has no price of its own (the parent of variations): a variation is subscribed instead.</summary>
    public const string NoOwnPrice = "NO_OWN_PRICE";
}
