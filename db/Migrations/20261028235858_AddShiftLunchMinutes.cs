using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftLunchMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LunchAvailableMinutes",
                table: "ShiftSeries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkedLunchMinutes",
                table: "ShiftSeries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LunchAvailableMinutes",
                table: "ShiftEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkedLunchMinutes",
                table: "ShiftEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LunchAvailableMinutes",
                table: "ShiftSeries");

            migrationBuilder.DropColumn(
                name: "WorkedLunchMinutes",
                table: "ShiftSeries");

            migrationBuilder.DropColumn(
                name: "LunchAvailableMinutes",
                table: "ShiftEntries");

            migrationBuilder.DropColumn(
                name: "WorkedLunchMinutes",
                table: "ShiftEntries");
        }
    }
}
