using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Unified.Db;

#nullable disable

namespace Unified.Db.Migrations
{
    [DbContext(typeof(UnifiedDbContext))]
    [Migration("20261028000000_AllowZeroAssignmentCapacity")]
    public partial class AllowZeroAssignmentCapacity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentEntries_CapacityAtLeastOne",
                table: "AssignmentEntries"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentSeries_CapacityAtLeastOne",
                table: "AssignmentSeries"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentEntries_CapacityNonNegative",
                table: "AssignmentEntries",
                sql: "\"Capacity\" >= 0"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentSeries_CapacityNonNegative",
                table: "AssignmentSeries",
                sql: "\"Capacity\" >= 0"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentEntries_CapacityNonNegative",
                table: "AssignmentEntries"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentSeries_CapacityNonNegative",
                table: "AssignmentSeries"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentEntries_CapacityAtLeastOne",
                table: "AssignmentEntries",
                sql: "\"Capacity\" >= 1"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentSeries_CapacityAtLeastOne",
                table: "AssignmentSeries",
                sql: "\"Capacity\" >= 1"
            );
        }
    }
}
