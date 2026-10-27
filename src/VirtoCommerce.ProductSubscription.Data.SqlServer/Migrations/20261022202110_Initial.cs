using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.ProductSubscription.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductSubscription",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OrganizationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProductId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PlanName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IntervalUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IntervalCount = table.Column<int>(type: "int", nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextOrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PausedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkipCount = table.Column<int>(type: "int", nullable: false),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ShippingAddressJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingMethodCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ShippingMethodOption = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PaymentMethodCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StoredPaymentMethodId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConsentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InitialOrderId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InitialOrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastOrderId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastOrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastOrderDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrdersCount = table.Column<int>(type: "int", nullable: false),
                    ReferencePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LastAnnualReminderDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentRenewalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OuterId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSubscription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionCartItem",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CartId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LineItemId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PlanId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IntervalUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IntervalCount = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionCartItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlan",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRenewal",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OrganizationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CutoffDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextProcessingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false),
                    PreviewTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NotifiedTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    OrderTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ShippingTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PaymentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PaymentMethodCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StoredPaymentMethodId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ShippingAddressJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingMethodCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ShippingMethodOption = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReminderSentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuthorizedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuthorizationExpiresDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CapturedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRenewal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanAssignment",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlanId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ObjectType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ObjectId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanAssignment_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanInterval",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlanId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanInterval", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanInterval_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRenewalEvent",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RenewalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRenewalEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionRenewalEvent_SubscriptionRenewal_RenewalId",
                        column: x => x.RenewalId,
                        principalTable: "SubscriptionRenewal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRenewalItem",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RenewalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ListPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    IsExcluded = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRenewalItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionRenewalItem_SubscriptionRenewal_RenewalId",
                        column: x => x.RenewalId,
                        principalTable: "SubscriptionRenewal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscription_CustomerId_StoreId",
                table: "ProductSubscription",
                columns: new[] { "CustomerId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscription_InitialOrderId",
                table: "ProductSubscription",
                column: "InitialOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscription_Number",
                table: "ProductSubscription",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscription_Status_NextOrderDate",
                table: "ProductSubscription",
                columns: new[] { "Status", "NextOrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionCartItem_CartId_LineItemId",
                table: "SubscriptionCartItem",
                columns: new[] { "CartId", "LineItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlan_StoreId_IsActive",
                table: "SubscriptionPlan",
                columns: new[] { "StoreId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanAssignment_ObjectType_ObjectId",
                table: "SubscriptionPlanAssignment",
                columns: new[] { "ObjectType", "ObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanAssignment_PlanId_Object",
                table: "SubscriptionPlanAssignment",
                columns: new[] { "PlanId", "ObjectType", "ObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanInterval_PlanId",
                table: "SubscriptionPlanInterval",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewal_CustomerId_StoreId",
                table: "SubscriptionRenewal",
                columns: new[] { "CustomerId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewal_IdempotencyKey",
                table: "SubscriptionRenewal",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewal_Number",
                table: "SubscriptionRenewal",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewal_OrderId",
                table: "SubscriptionRenewal",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewal_State_NextProcessingDate",
                table: "SubscriptionRenewal",
                columns: new[] { "State", "NextProcessingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewalEvent_RenewalId",
                table: "SubscriptionRenewalEvent",
                column: "RenewalId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewalItem_RenewalId",
                table: "SubscriptionRenewalItem",
                column: "RenewalId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewalItem_SubscriptionId",
                table: "SubscriptionRenewalItem",
                column: "SubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductSubscription");

            migrationBuilder.DropTable(
                name: "SubscriptionCartItem");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanAssignment");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanInterval");

            migrationBuilder.DropTable(
                name: "SubscriptionRenewalEvent");

            migrationBuilder.DropTable(
                name: "SubscriptionRenewalItem");

            migrationBuilder.DropTable(
                name: "SubscriptionPlan");

            migrationBuilder.DropTable(
                name: "SubscriptionRenewal");
        }
    }
}
