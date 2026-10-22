using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class SubscriptionEntity : AuditableEntity, IDataEntity<SubscriptionEntity, Subscription>
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

    [Required]
    [StringLength(128)]
    public string ProductId { get; set; }

    [StringLength(128)]
    public string Sku { get; set; }

    [StringLength(1024)]
    public string ProductName { get; set; }

    [StringLength(2048)]
    public string ImageUrl { get; set; }

    public int Quantity { get; set; }

    [StringLength(128)]
    public string PlanId { get; set; }

    [StringLength(256)]
    public string PlanName { get; set; }

    [Required]
    [StringLength(16)]
    public string IntervalUnit { get; set; }

    public int IntervalCount { get; set; }

    [StringLength(32)]
    public string DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    [Required]
    [StringLength(32)]
    public string Status { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime NextOrderDate { get; set; }
    public DateTime? PausedUntil { get; set; }
    public int SkipCount { get; set; }
    public DateTime? CancelledDate { get; set; }

    [StringLength(2048)]
    public string CancelReason { get; set; }

    [StringLength(16)]
    public string Currency { get; set; }

    [StringLength(16)]
    public string LanguageCode { get; set; }

    public string ShippingAddressJson { get; set; }

    [StringLength(128)]
    public string ShippingMethodCode { get; set; }

    [StringLength(128)]
    public string ShippingMethodOption { get; set; }

    [StringLength(128)]
    public string PaymentMethodCode { get; set; }

    [StringLength(128)]
    public string StoredPaymentMethodId { get; set; }

    [StringLength(128)]
    public string ConsentId { get; set; }

    [StringLength(128)]
    public string InitialOrderId { get; set; }

    [StringLength(64)]
    public string InitialOrderNumber { get; set; }

    [StringLength(128)]
    public string LastOrderId { get; set; }

    [StringLength(64)]
    public string LastOrderNumber { get; set; }

    public DateTime? LastOrderDate { get; set; }
    public int OrdersCount { get; set; }
    public decimal? ReferencePrice { get; set; }
    public DateTime? LastAnnualReminderDate { get; set; }

    [StringLength(128)]
    public string CurrentRenewalId { get; set; }

    [StringLength(128)]
    public string OuterId { get; set; }

    public virtual Subscription ToModel(Subscription model)
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
        model.ProductId = ProductId;
        model.Sku = Sku;
        model.ProductName = ProductName;
        model.ImageUrl = ImageUrl;
        model.Quantity = Quantity;
        model.PlanId = PlanId;
        model.PlanName = PlanName;
        model.IntervalUnit = EnumUtility.SafeParse(IntervalUnit, SubscriptionIntervalUnit.Month);
        model.IntervalCount = IntervalCount;
        model.DiscountType = EnumUtility.SafeParse(DiscountType, SubscriptionDiscountType.None);
        model.DiscountValue = DiscountValue;
        model.Status = Status;
        model.StartDate = StartDate;
        model.NextOrderDate = NextOrderDate;
        model.PausedUntil = PausedUntil;
        model.SkipCount = SkipCount;
        model.CancelledDate = CancelledDate;
        model.CancelReason = CancelReason;
        model.Currency = Currency;
        model.LanguageCode = LanguageCode;
        model.ShippingAddress = string.IsNullOrEmpty(ShippingAddressJson) ? null : JsonConvert.DeserializeObject<Address>(ShippingAddressJson);
        model.ShippingMethodCode = ShippingMethodCode;
        model.ShippingMethodOption = ShippingMethodOption;
        model.PaymentMethodCode = PaymentMethodCode;
        model.StoredPaymentMethodId = StoredPaymentMethodId;
        model.ConsentId = ConsentId;
        model.InitialOrderId = InitialOrderId;
        model.InitialOrderNumber = InitialOrderNumber;
        model.LastOrderId = LastOrderId;
        model.LastOrderNumber = LastOrderNumber;
        model.LastOrderDate = LastOrderDate;
        model.OrdersCount = OrdersCount;
        model.ReferencePrice = ReferencePrice;
        model.LastAnnualReminderDate = LastAnnualReminderDate;
        model.CurrentRenewalId = CurrentRenewalId;
        model.OuterId = OuterId;

        return model;
    }

    public virtual SubscriptionEntity FromModel(Subscription model, PrimaryKeyResolvingMap pkMap)
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
        ProductId = model.ProductId;
        Sku = model.Sku;
        ProductName = model.ProductName;
        ImageUrl = model.ImageUrl;
        Quantity = model.Quantity;
        PlanId = model.PlanId;
        PlanName = model.PlanName;
        IntervalUnit = model.IntervalUnit.ToString();
        IntervalCount = model.IntervalCount;
        DiscountType = model.DiscountType.ToString();
        DiscountValue = model.DiscountValue;
        Status = model.Status;
        StartDate = model.StartDate;
        NextOrderDate = model.NextOrderDate;
        PausedUntil = model.PausedUntil;
        SkipCount = model.SkipCount;
        CancelledDate = model.CancelledDate;
        CancelReason = model.CancelReason;
        Currency = model.Currency;
        LanguageCode = model.LanguageCode;
        ShippingAddressJson = model.ShippingAddress == null ? null : JsonConvert.SerializeObject(model.ShippingAddress);
        ShippingMethodCode = model.ShippingMethodCode;
        ShippingMethodOption = model.ShippingMethodOption;
        PaymentMethodCode = model.PaymentMethodCode;
        StoredPaymentMethodId = model.StoredPaymentMethodId;
        ConsentId = model.ConsentId;
        InitialOrderId = model.InitialOrderId;
        InitialOrderNumber = model.InitialOrderNumber;
        LastOrderId = model.LastOrderId;
        LastOrderNumber = model.LastOrderNumber;
        LastOrderDate = model.LastOrderDate;
        OrdersCount = model.OrdersCount;
        ReferencePrice = model.ReferencePrice;
        LastAnnualReminderDate = model.LastAnnualReminderDate;
        CurrentRenewalId = model.CurrentRenewalId;
        OuterId = model.OuterId;

        return this;
    }

    public virtual void Patch(SubscriptionEntity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Number = Number;
        target.StoreId = StoreId;
        target.CustomerId = CustomerId;
        target.CustomerName = CustomerName;
        target.CustomerEmail = CustomerEmail;
        target.OrganizationId = OrganizationId;
        target.ProductId = ProductId;
        target.Sku = Sku;
        target.ProductName = ProductName;
        target.ImageUrl = ImageUrl;
        target.Quantity = Quantity;
        target.PlanId = PlanId;
        target.PlanName = PlanName;
        target.IntervalUnit = IntervalUnit;
        target.IntervalCount = IntervalCount;
        target.DiscountType = DiscountType;
        target.DiscountValue = DiscountValue;
        target.Status = Status;
        target.StartDate = StartDate;
        target.NextOrderDate = NextOrderDate;
        target.PausedUntil = PausedUntil;
        target.SkipCount = SkipCount;
        target.CancelledDate = CancelledDate;
        target.CancelReason = CancelReason;
        target.Currency = Currency;
        target.LanguageCode = LanguageCode;
        target.ShippingAddressJson = ShippingAddressJson;
        target.ShippingMethodCode = ShippingMethodCode;
        target.ShippingMethodOption = ShippingMethodOption;
        target.PaymentMethodCode = PaymentMethodCode;
        target.StoredPaymentMethodId = StoredPaymentMethodId;
        target.ConsentId = ConsentId;
        target.InitialOrderId = InitialOrderId;
        target.InitialOrderNumber = InitialOrderNumber;
        target.LastOrderId = LastOrderId;
        target.LastOrderNumber = LastOrderNumber;
        target.LastOrderDate = LastOrderDate;
        target.OrdersCount = OrdersCount;
        target.ReferencePrice = ReferencePrice;
        target.LastAnnualReminderDate = LastAnnualReminderDate;
        target.CurrentRenewalId = CurrentRenewalId;
        target.OuterId = OuterId;
    }
}
