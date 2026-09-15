using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class MakeValueNullableAndAddIsRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "SubCategoryMetrics",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "StatRecords",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsRequired", table: "SubCategoryMetrics");

            // Set any null values to 0 before making the column non-nullable
            migrationBuilder.Sql("""UPDATE "StatRecords" SET "Value" = 0 WHERE "Value" IS NULL""");

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "StatRecords",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true
            );
        }
    }
}
