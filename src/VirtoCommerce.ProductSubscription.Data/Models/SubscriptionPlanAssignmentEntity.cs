using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.ProductSubscription.Data.Models;

public class SubscriptionPlanAssignmentEntity : Entity
{
    public const string ProductObjectType = "Product";
    public const string CategoryObjectType = "Category";

    [Required]
    [StringLength(128)]
    public string PlanId { get; set; }
    public virtual SubscriptionPlanEntity Plan { get; set; }

    [Required]
    [StringLength(32)]
    public string ObjectType { get; set; }

    [Required]
    [StringLength(128)]
    public string ObjectId { get; set; }
}
