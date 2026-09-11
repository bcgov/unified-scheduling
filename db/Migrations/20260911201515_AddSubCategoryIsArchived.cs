using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSubCategoryIsArchived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SubCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SubCategories");
        }
    }
}
