using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.ProductSubscription.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionOuterIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscription_OuterId",
                table: "ProductSubscription",
                column: "OuterId",
                unique: true,
                filter: "[OuterId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductSubscription_OuterId",
                table: "ProductSubscription");
        }
    }
}
