using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class RenewalEntity : AuditableEntity, IDataEntity<RenewalEntity, Renewal>
{
    [Required]
    [StringLength(64)]
    public string Number { get; set; }

    [Required]
    [StringLength(128)]
    public string StoreId { get; set; }

    [Required]
    [StringLength(128)]
    public string CustomerId { get; set; }

    [StringLength(512)]
    public string CustomerName { get; set; }

    [StringLength(256)]
    public string CustomerEmail { get; set; }

    [StringLength(128)]
    public string OrganizationId { get; set; }

    [StringLength(16)]
    public string Currency { get; set; }

    [StringLength(16)]
    public string LanguageCode { get; set; }

    public DateTime ScheduledDate { get; set; }
    public DateTime CutoffDate { get; set; }
    public DateTime? NextProcessingDate { get; set; }

    [Required]
    [StringLength(32)]
    public string State { get; set; }

    public bool IsManual { get; set; }

    public decimal PreviewTotal { get; set; }
    public decimal? NotifiedTotal { get; set; }
    public decimal? OrderTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }

    [StringLength(128)]
    public string OrderId { get; set; }

    [StringLength(64)]
    public string OrderNumber { get; set; }

    [StringLength(128)]
    public string PaymentId { get; set; }

    [StringLength(128)]
    public string PaymentMethodCode { get; set; }

    [StringLength(128)]
    public string StoredPaymentMethodId { get; set; }

    public string ShippingAddressJson { get; set; }

    [StringLength(128)]
    public string ShippingMethodCode { get; set; }

    [StringLength(128)]
    public string ShippingMethodOption { get; set; }

    public int AttemptCount { get; set; }
    public DateTime? NextAttemptDate { get; set; }

    [StringLength(32)]
    public string LastOutcome { get; set; }

    [StringLength(1024)]
    public string LastErrorMessage { get; set; }

    [StringLength(1024)]
    public string ReviewReason { get; set; }

    [StringLength(128)]
    public string IdempotencyKey { get; set; }

    public DateTime? ReminderSentDate { get; set; }
    public DateTime? AuthorizedDate { get; set; }
    public DateTime? AuthorizationExpiresDate { get; set; }
    public DateTime? CapturedDate { get; set; }

    public virtual ObservableCollection<RenewalItemEntity> Items { get; set; } = new NullCollection<RenewalItemEntity>();
    public virtual ObservableCollection<RenewalEventEntity> Events { get; set; } = new NullCollection<RenewalEventEntity>();

    public virtual Renewal ToModel(Renewal model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.Number = Number;
        model.StoreId = StoreId;
        model.CustomerId = CustomerId;
        model.CustomerName = CustomerName;
        model.CustomerEmail = CustomerEmail;
        model.OrganizationId = OrganizationId;
        model.Currency = Currency;
        model.LanguageCode = LanguageCode;
        model.ScheduledDate = ScheduledDate;
        model.CutoffDate = CutoffDate;
        model.NextProcessingDate = NextProcessingDate;
        model.State = State;
        model.IsManual = IsManual;
        model.PreviewTotal = PreviewTotal;
        model.NotifiedTotal = NotifiedTotal;
        model.OrderTotal = OrderTotal;
        model.DiscountTotal = DiscountTotal;
        model.ShippingTotal = ShippingTotal;
        model.TaxTotal = TaxTotal;
        model.OrderId = OrderId;
        model.OrderNumber = OrderNumber;
        model.PaymentId = PaymentId;
        model.PaymentMethodCode = PaymentMethodCode;
        model.StoredPaymentMethodId = StoredPaymentMethodId;
        model.ShippingAddress = string.IsNullOrEmpty(ShippingAddressJson) ? null : JsonConvert.DeserializeObject<Address>(ShippingAddressJson);
        model.ShippingMethodCode = ShippingMethodCode;
        model.ShippingMethodOption = ShippingMethodOption;
        model.AttemptCount = AttemptCount;
        model.NextAttemptDate = NextAttemptDate;
        model.LastOutcome = LastOutcome;
        model.LastErrorMessage = LastErrorMessage;
        model.ReviewReason = ReviewReason;
        model.IdempotencyKey = IdempotencyKey;
        model.ReminderSentDate = ReminderSentDate;
        model.AuthorizedDate = AuthorizedDate;
        model.AuthorizationExpiresDate = AuthorizationExpiresDate;
        model.CapturedDate = CapturedDate;

        model.Items = Items.OrderBy(x => x.SortOrder).Select(x => x.ToModel(AbstractTypeFactory<RenewalItem>.TryCreateInstance())).ToList();
        model.Events = Events.OrderBy(x => x.EventDate).ThenBy(x => x.Sequence).Select(x => x.ToModel(AbstractTypeFactory<RenewalEvent>.TryCreateInstance())).ToList();

        return model;
    }

    public virtual RenewalEntity FromModel(Renewal model, PrimaryKeyResolvingMap pkMap)
    {
        ArgumentNullException.ThrowIfNull(model);

        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        Number = model.Number;
        StoreId = model.StoreId;
        CustomerId = model.CustomerId;
        CustomerName = model.CustomerName;
        CustomerEmail = model.CustomerEmail;
        OrganizationId = model.OrganizationId;
        Currency = model.Currency;
        LanguageCode = model.LanguageCode;
        ScheduledDate = model.ScheduledDate;
        CutoffDate = model.CutoffDate;
        NextProcessingDate = model.NextProcessingDate;
        State = model.State;
        IsManual = model.IsManual;
        PreviewTotal = model.PreviewTotal;
        NotifiedTotal = model.NotifiedTotal;
        OrderTotal = model.OrderTotal;
        DiscountTotal = model.DiscountTotal;
        ShippingTotal = model.ShippingTotal;
        TaxTotal = model.TaxTotal;
        OrderId = model.OrderId;
        OrderNumber = model.OrderNumber;
        PaymentId = model.PaymentId;
        PaymentMethodCode = model.PaymentMethodCode;
        StoredPaymentMethodId = model.StoredPaymentMethodId;
        ShippingAddressJson = model.ShippingAddress == null ? null : JsonConvert.SerializeObject(model.ShippingAddress);
        ShippingMethodCode = model.ShippingMethodCode;
        ShippingMethodOption = model.ShippingMethodOption;
        AttemptCount = model.AttemptCount;
        NextAttemptDate = model.NextAttemptDate;
        LastOutcome = model.LastOutcome;
        LastErrorMessage = model.LastErrorMessage;
        ReviewReason = model.ReviewReason;
        IdempotencyKey = model.IdempotencyKey;
        ReminderSentDate = model.ReminderSentDate;
        AuthorizedDate = model.AuthorizedDate;
        AuthorizationExpiresDate = model.AuthorizationExpiresDate;
        CapturedDate = model.CapturedDate;

        if (model.Items != null)
        {
            Items = new ObservableCollection<RenewalItemEntity>(model.Items.Select((x, i) =>
            {
                var entity = AbstractTypeFactory<RenewalItemEntity>.TryCreateInstance().FromModel(x, pkMap);
                entity.SortOrder = i;
                return entity;
            }));
        }

        if (model.Events != null)
        {
            Events = new ObservableCollection<RenewalEventEntity>(model.Events.Select((x, i) =>
            {
                var entity = AbstractTypeFactory<RenewalEventEntity>.TryCreateInstance().FromModel(x, pkMap);
                entity.Sequence = i;
                return entity;
            }));
        }

        return this;
    }

    public virtual void Patch(RenewalEntity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Number = Number;
        target.StoreId = StoreId;
        target.CustomerId = CustomerId;
        target.CustomerName = CustomerName;
        target.CustomerEmail = CustomerEmail;
        target.OrganizationId = OrganizationId;
        target.Currency = Currency;
        target.LanguageCode = LanguageCode;
        target.ScheduledDate = ScheduledDate;
        target.CutoffDate = CutoffDate;
        target.NextProcessingDate = NextProcessingDate;
        target.State = State;
        target.IsManual = IsManual;
        target.PreviewTotal = PreviewTotal;
        target.NotifiedTotal = NotifiedTotal;
        target.OrderTotal = OrderTotal;
        target.DiscountTotal = DiscountTotal;
        target.ShippingTotal = ShippingTotal;
        target.TaxTotal = TaxTotal;
        target.OrderId = OrderId;
        target.OrderNumber = OrderNumber;
        target.PaymentId = PaymentId;
        target.PaymentMethodCode = PaymentMethodCode;
        target.StoredPaymentMethodId = StoredPaymentMethodId;
        target.ShippingAddressJson = ShippingAddressJson;
        target.ShippingMethodCode = ShippingMethodCode;
        target.ShippingMethodOption = ShippingMethodOption;
        target.AttemptCount = AttemptCount;
        target.NextAttemptDate = NextAttemptDate;
        target.LastOutcome = LastOutcome;
        target.LastErrorMessage = LastErrorMessage;
        target.ReviewReason = ReviewReason;
        target.IdempotencyKey = IdempotencyKey;
        target.ReminderSentDate = ReminderSentDate;
        target.AuthorizedDate = AuthorizedDate;
        target.AuthorizationExpiresDate = AuthorizationExpiresDate;
        target.CapturedDate = CapturedDate;

        if (!Items.IsNullCollection())
        {
            Items.Patch(target.Items, (source, destination) => source.Patch(destination));
        }

        if (!Events.IsNullCollection())
        {
            // The event log is append-only: add new events, never change or remove existing ones.
            foreach (var newEvent in Events.Where(x => x.IsTransient()))
            {
                target.Events.Add(newEvent);
            }
        }
    }
}
