using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.ProductSubscription.Data.MySql.Migrations
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
                unique: true);
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
