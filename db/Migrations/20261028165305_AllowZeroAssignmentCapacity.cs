using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AllowZeroAssignmentCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentSeries_CapacityAtLeastOne",
                table: "AssignmentSeries"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentEntries_CapacityAtLeastOne",
                table: "AssignmentEntries"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentSeries_CapacityNonNegative",
                table: "AssignmentSeries",
                sql: "\"Capacity\" >= 0"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentEntries_CapacityNonNegative",
                table: "AssignmentEntries",
                sql: "\"Capacity\" >= 0"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentSeries_CapacityNonNegative",
                table: "AssignmentSeries"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentEntries_CapacityNonNegative",
                table: "AssignmentEntries"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentSeries_CapacityAtLeastOne",
                table: "AssignmentSeries",
                sql: "\"Capacity\" >= 1"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentEntries_CapacityAtLeastOne",
                table: "AssignmentEntries",
                sql: "\"Capacity\" >= 1"
            );
        }
    }
}
