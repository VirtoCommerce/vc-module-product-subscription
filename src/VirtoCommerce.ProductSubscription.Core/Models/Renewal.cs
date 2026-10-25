using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Core.Models;

/// <summary>
/// One renewal run: the subscriptions of one customer that are due together, moving through
/// Scheduled → Notified → (ReviewRequired) → Ordered → Authorized → Completed, or to Skipped / Failed / Cancelled.
/// </summary>
public class Renewal : AuditableEntity, ICloneable
{
    public string Number { get; set; }
    public string StoreId { get; set; }

    public string CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
    public string OrganizationId { get; set; }

    public string Currency { get; set; }
    public string LanguageCode { get; set; }

    /// <summary>The delivery date the renewal is for (the subscriptions' next order date).</summary>
    public DateTime ScheduledDate { get; set; }

    /// <summary>After this moment the renewal is locked: prices are re-checked and the order is placed.</summary>
    public DateTime CutoffDate { get; set; }

    /// <summary>When the scheduler should look at this renewal again.</summary>
    public DateTime? NextProcessingDate { get; set; }

    public string State { get; set; } = RenewalState.Scheduled;

    /// <summary>True for "order now" renewals started by the customer or an admin.</summary>
    public bool IsManual { get; set; }

    public IList<RenewalItem> Items { get; set; } = new List<RenewalItem>();

    public decimal PreviewTotal { get; set; }

    /// <summary>Total shown in the reminder. The renewal is never charged more than this without the customer's review.</summary>
    public decimal? NotifiedTotal { get; set; }

    public decimal? OrderTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }

    public string OrderId { get; set; }
    public string OrderNumber { get; set; }
    public string PaymentId { get; set; }

    public string PaymentMethodCode { get; set; }
    public string StoredPaymentMethodId { get; set; }

    public Address ShippingAddress { get; set; }
    public string ShippingMethodCode { get; set; }
    public string ShippingMethodOption { get; set; }

    public int AttemptCount { get; set; }
    public DateTime? NextAttemptDate { get; set; }
    public string LastOutcome { get; set; }

    /// <summary>Customer-safe explanation of the last problem (never raw provider codes).</summary>
    public string LastErrorMessage { get; set; }

    public string ReviewReason { get; set; }

    public string IdempotencyKey { get; set; }

    public DateTime? ReminderSentDate { get; set; }
    public DateTime? AuthorizedDate { get; set; }
    public DateTime? AuthorizationExpiresDate { get; set; }
    public DateTime? CapturedDate { get; set; }

    public IList<RenewalEvent> Events { get; set; } = new List<RenewalEvent>();

    public virtual bool IsOpen => !RenewalState.Final.Contains(State);

    public virtual void AddEvent(string eventType, string message, string details = null)
    {
        Events ??= new List<RenewalEvent>();
        Events.Add(new RenewalEvent
        {
            EventDate = DateTime.UtcNow,
            State = State,
            EventType = eventType,
            Message = message,
            Details = details,
        });
    }

    public virtual void ChangeState(string newState, string message, DateTime? nextProcessingDate = null)
    {
        var previous = State;
        State = newState;
        NextProcessingDate = nextProcessingDate;
        AddEvent(RenewalEventType.StateChanged, message, $"{previous} → {newState}");
    }

    public virtual object Clone()
    {
        var result = (Renewal)MemberwiseClone();
        result.Items = Items?.Select(x => (RenewalItem)x.Clone()).ToList();
        result.Events = Events?.Select(x => (RenewalEvent)x.Clone()).ToList();
        result.ShippingAddress = (Address)ShippingAddress?.Clone();
        return result;
    }
}
