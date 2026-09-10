using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformedAtLocationAndIsLocationLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerformedAtLocationId",
                table: "StatRecords",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_StatRecords_PerformedAtLocationId",
                table: "StatRecords",
                column: "PerformedAtLocationId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_StatRecords_Locations_PerformedAtLocationId",
                table: "StatRecords",
                column: "PerformedAtLocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StatRecords_Locations_PerformedAtLocationId",
                table: "StatRecords"
            );

            migrationBuilder.DropIndex(
                name: "IX_StatRecords_PerformedAtLocationId",
                table: "StatRecords"
            );

            migrationBuilder.DropColumn(name: "PerformedAtLocationId", table: "StatRecords");
        }
    }
}
