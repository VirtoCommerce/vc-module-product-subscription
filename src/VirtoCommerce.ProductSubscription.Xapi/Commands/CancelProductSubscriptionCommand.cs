namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

public class CancelProductSubscriptionCommand : SubscriptionIdCommand
{
    public string Reason { get; set; }
}
