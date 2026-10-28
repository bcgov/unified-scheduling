using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddExceptionReasonColumnToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoanOutEntries_LoanOutSeries_LoanOutSeriesId",
                table: "LoanOutEntries"
            );

            migrationBuilder.AddColumn<string>(
                name: "ExceptionReason",
                table: "Events",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LoanOutEntries_LoanOutSeries_LoanOutSeriesId",
                table: "LoanOutEntries",
                column: "LoanOutSeriesId",
                principalTable: "LoanOutSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoanOutEntries_LoanOutSeries_LoanOutSeriesId",
                table: "LoanOutEntries"
            );

            migrationBuilder.DropColumn(name: "ExceptionReason", table: "Events");

            migrationBuilder.AddForeignKey(
                name: "FK_LoanOutEntries_LoanOutSeries_LoanOutSeriesId",
                table: "LoanOutEntries",
                column: "LoanOutSeriesId",
                principalTable: "LoanOutSeries",
                principalColumn: "Id"
            );
        }
    }
}
