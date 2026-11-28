using System;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;

namespace VirtoCommerce.ProductSubscription.ConnectorTestKit;

/// <summary>
/// Optional features of the stored credential flow. A feature the connector does not support must fail explicitly
/// (an unsuccessful result or <see cref="NotSupportedException"/>), never succeed silently or throw <see cref="NotImplementedException"/>.
/// </summary>
public class ConnectorCapabilities
{
    /// <summary><see cref="StoredPaymentCaptureMode.AuthorizeOnly"/>: authorize now, capture when the order ships (what renewals use).</summary>
    public bool SupportsAuthorizeOnly { get; set; } = true;

    /// <summary><see cref="StoredPaymentCaptureMode.AuthorizeAndCapture"/> in one call.</summary>
    public bool SupportsAuthorizeAndCapture { get; set; } = true;

    /// <summary>Capturing an authorized merchant-initiated charge with <c>CaptureProcessPaymentAsync</c>.</summary>
    public bool SupportsCapture { get; set; } = true;

    /// <summary>Deleting the stored method at the provider with <c>DeleteStoredPaymentMethodAsync</c>.</summary>
    public bool SupportsDelete { get; set; } = true;

    /// <summary>The sandbox rejects charges against a deleted token (some sandboxes don't enforce it).</summary>
    public bool RejectsDeletedToken { get; set; } = true;
}
