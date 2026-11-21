using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class RenewalEventEntity : Entity, IDataEntity<RenewalEventEntity, RenewalEvent>
{
    [Required]
    public const int MessageLength = 1024;
    public const int DetailsLength = 2048;

    [StringLength(128)]
    public string RenewalId { get; set; }
    public virtual RenewalEntity Renewal { get; set; }

    public DateTime EventDate { get; set; }
    public int Sequence { get; set; }

    [StringLength(32)]
    public string State { get; set; }

    [StringLength(64)]
    public string EventType { get; set; }

    [StringLength(MessageLength)]
    public string Message { get; set; }

    [StringLength(DetailsLength)]
    public string Details { get; set; }

    public virtual RenewalEvent ToModel(RenewalEvent model)
    {
        model.Id = Id;
        model.EventDate = EventDate;
        model.State = State;
        model.EventType = EventType;
        model.Message = Message;
        model.Details = Details;
        return model;
    }

    public virtual RenewalEventEntity FromModel(RenewalEvent model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);
        Id = model.Id;
        EventDate = model.EventDate;
        State = model.State;
        EventType = model.EventType;
        Message = Truncate(model.Message, MessageLength);
        Details = Truncate(model.Details, DetailsLength);
        return this;
    }

    public virtual void Patch(RenewalEventEntity target)
    {
        // Events are immutable.
    }

    private static string Truncate(string value, int maxLength)
    {
        return value == null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
