using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.ProductSubscription.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAnchorDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnchorDay",
                table: "ProductSubscription",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnchorDay",
                table: "ProductSubscription");
        }
    }
}
