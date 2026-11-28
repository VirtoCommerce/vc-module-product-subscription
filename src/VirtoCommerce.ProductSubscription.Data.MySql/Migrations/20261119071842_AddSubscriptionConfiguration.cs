using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.ProductSubscription.Data.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigurationHash",
                table: "SubscriptionRenewalItem",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConfigurationJson",
                table: "SubscriptionRenewalItem",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsConfigurationChanged",
                table: "SubscriptionRenewalItem",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ConfigurationHash",
                table: "SubscriptionCartItem",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConfigurationHash",
                table: "ProductSubscription",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConfigurationJson",
                table: "ProductSubscription",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewalItem_ProductId_ConfigurationHash",
                table: "SubscriptionRenewalItem",
                columns: new[] { "ProductId", "ConfigurationHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscription_ProductId_ConfigurationHash",
                table: "ProductSubscription",
                columns: new[] { "ProductId", "ConfigurationHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionRenewalItem_ProductId_ConfigurationHash",
                table: "SubscriptionRenewalItem");

            migrationBuilder.DropIndex(
                name: "IX_ProductSubscription_ProductId_ConfigurationHash",
                table: "ProductSubscription");

            migrationBuilder.DropColumn(
                name: "ConfigurationHash",
                table: "SubscriptionRenewalItem");

            migrationBuilder.DropColumn(
                name: "ConfigurationJson",
                table: "SubscriptionRenewalItem");

            migrationBuilder.DropColumn(
                name: "IsConfigurationChanged",
                table: "SubscriptionRenewalItem");

            migrationBuilder.DropColumn(
                name: "ConfigurationHash",
                table: "SubscriptionCartItem");

            migrationBuilder.DropColumn(
                name: "ConfigurationHash",
                table: "ProductSubscription");

            migrationBuilder.DropColumn(
                name: "ConfigurationJson",
                table: "ProductSubscription");
        }
    }
}
