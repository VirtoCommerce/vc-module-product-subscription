using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.ProductSubscription.Data.Models;

namespace VirtoCommerce.ProductSubscription.Data.Repositories;

public class ProductSubscriptionDbContext : DbContextBase
{
    private const int MoneyPrecision = 18;
    private const int MoneyScale = 4;

    public ProductSubscriptionDbContext(DbContextOptions<ProductSubscriptionDbContext> options)
        : base(options)
    {
    }

    protected ProductSubscriptionDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscriptionPlanEntity>(builder =>
        {
            builder.ToTable("SubscriptionPlan").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.Property(x => x.DiscountValue).HasPrecision(MoneyPrecision, MoneyScale);
            builder.HasIndex(x => new { x.StoreId, x.IsActive }).HasDatabaseName("IX_SubscriptionPlan_StoreId_IsActive");
        });

        modelBuilder.Entity<SubscriptionPlanIntervalEntity>(builder =>
        {
            builder.ToTable("SubscriptionPlanInterval").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.HasOne(x => x.Plan).WithMany(x => x.Intervals).HasForeignKey(x => x.PlanId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubscriptionPlanAssignmentEntity>(builder =>
        {
            builder.ToTable("SubscriptionPlanAssignment").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.HasOne(x => x.Plan).WithMany(x => x.Assignments).HasForeignKey(x => x.PlanId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.ObjectType, x.ObjectId }).HasDatabaseName("IX_SubscriptionPlanAssignment_ObjectType_ObjectId");
            builder.HasIndex(x => new { x.PlanId, x.ObjectType, x.ObjectId }).IsUnique().HasDatabaseName("IX_SubscriptionPlanAssignment_PlanId_Object");
        });

        modelBuilder.Entity<SubscriptionEntity>(builder =>
        {
            builder.ToTable("ProductSubscription").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.Property(x => x.DiscountValue).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.ReferencePrice).HasPrecision(MoneyPrecision, MoneyScale);
            builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_ProductSubscription_Number");
            builder.HasIndex(x => new { x.CustomerId, x.StoreId }).HasDatabaseName("IX_ProductSubscription_CustomerId_StoreId");
            builder.HasIndex(x => new { x.Status, x.NextOrderDate }).HasDatabaseName("IX_ProductSubscription_Status_NextOrderDate");
            builder.HasIndex(x => x.InitialOrderId).HasDatabaseName("IX_ProductSubscription_InitialOrderId");
            // The legacy import is idempotent by OuterId: one subscription per legacy subscription.
            builder.HasIndex(x => x.OuterId).IsUnique().HasDatabaseName("IX_ProductSubscription_OuterId");
        });

        modelBuilder.Entity<RenewalEntity>(builder =>
        {
            builder.ToTable("SubscriptionRenewal").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.Property(x => x.PreviewTotal).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.NotifiedTotal).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.OrderTotal).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.DiscountTotal).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.ShippingTotal).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.TaxTotal).HasPrecision(MoneyPrecision, MoneyScale);
            builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("IX_SubscriptionRenewal_Number");
            builder.HasIndex(x => new { x.State, x.NextProcessingDate }).HasDatabaseName("IX_SubscriptionRenewal_State_NextProcessingDate");
            builder.HasIndex(x => new { x.CustomerId, x.StoreId }).HasDatabaseName("IX_SubscriptionRenewal_CustomerId_StoreId");
            builder.HasIndex(x => x.OrderId).HasDatabaseName("IX_SubscriptionRenewal_OrderId");
            builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("IX_SubscriptionRenewal_IdempotencyKey");
        });

        modelBuilder.Entity<RenewalItemEntity>(builder =>
        {
            builder.ToTable("SubscriptionRenewalItem").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.Property(x => x.ListPrice).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.DiscountAmount).HasPrecision(MoneyPrecision, MoneyScale);
            builder.Property(x => x.Total).HasPrecision(MoneyPrecision, MoneyScale);
            builder.HasOne(x => x.Renewal).WithMany(x => x.Items).HasForeignKey(x => x.RenewalId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => x.SubscriptionId).HasDatabaseName("IX_SubscriptionRenewalItem_SubscriptionId");
        });

        modelBuilder.Entity<RenewalEventEntity>(builder =>
        {
            builder.ToTable("SubscriptionRenewalEvent").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.HasOne(x => x.Renewal).WithMany(x => x.Events).HasForeignKey(x => x.RenewalId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubscriptionCartItemEntity>(builder =>
        {
            builder.ToTable("SubscriptionCartItem").HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
            builder.HasIndex(x => new { x.CartId, x.LineItemId }).IsUnique().HasDatabaseName("IX_SubscriptionCartItem_CartId_LineItemId");
        });

        base.OnModelCreating(modelBuilder);

        switch (Database.ProviderName)
        {
            case "Pomelo.EntityFrameworkCore.MySql":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.ProductSubscription.Data.MySql"));
                break;
            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.ProductSubscription.Data.PostgreSql"));
                break;
            case "Microsoft.EntityFrameworkCore.SqlServer":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.ProductSubscription.Data.SqlServer"));
                break;
        }
    }
}
