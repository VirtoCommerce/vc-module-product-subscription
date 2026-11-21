using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.ProductSubscription.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalReasonCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "SubscriptionRenewal",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReasonCode",
                table: "SubscriptionRenewal",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "SubscriptionRenewal");

            migrationBuilder.DropColumn(
                name: "ReviewReasonCode",
                table: "SubscriptionRenewal");
        }
    }
}
