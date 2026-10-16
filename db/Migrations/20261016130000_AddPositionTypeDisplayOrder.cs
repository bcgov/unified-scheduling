using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Unified.Db;

#nullable disable

namespace Unified.Db.Migrations
{
    [DbContext(typeof(UnifiedDbContext))]
    [Migration("20261016130000_AddPositionTypeDisplayOrder")]
    public partial class AddPositionTypeDisplayOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "PositionTypes",
                type: "integer",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "PositionTypes"
                SET "DisplayOrder" = CASE "Code"
                    WHEN 'Chief Sheriff' THEN 1
                    WHEN 'Deputy Chief Sheriff' THEN 2
                    WHEN 'Superintendent' THEN 3
                    WHEN 'Senior Inspector' THEN 4
                    WHEN 'Staff Inspector' THEN 5
                    WHEN 'Inspector' THEN 6
                    WHEN 'Staff Sergeant' THEN 7
                    WHEN 'Sergeant' THEN 8
                    WHEN 'Deputy Sheriff' THEN 9
                    ELSE 2147483647
                END;
                """
            );

            migrationBuilder.AlterColumn<int>(
                name: "DisplayOrder",
                table: "PositionTypes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_PositionTypes_DisplayOrderPositive",
                table: "PositionTypes",
                sql: "\"DisplayOrder\" > 0"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DisplayOrder", table: "PositionTypes");
        }
    }
}
