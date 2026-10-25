namespace VirtoCommerce.ProductSubscription.Data.Handlers;

/// <summary>The order whose change <see cref="OrderChangedJob"/> processes.</summary>
public class OrderChangedJobPayload
{
    public string OrderId { get; set; }

    /// <summary>True when the job was enqueued for the order's creation.</summary>
    public bool IsNew { get; set; }
}
