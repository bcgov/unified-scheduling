using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class _20260622000000_AddSignedOffAuditToStatRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SignedOffAt",
                table: "StatRecords",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "SignedOffByUserId",
                table: "StatRecords",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_StatRecords_SignedOffByUserId",
                table: "StatRecords",
                column: "SignedOffByUserId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_StatRecords_Users_SignedOffByUserId",
                table: "StatRecords",
                column: "SignedOffByUserId",
                principalTable: "Users",
                principalColumn: "Id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StatRecords_Users_SignedOffByUserId",
                table: "StatRecords"
            );

            migrationBuilder.DropIndex(
                name: "IX_StatRecords_SignedOffByUserId",
                table: "StatRecords"
            );

            migrationBuilder.DropColumn(name: "SignedOffAt", table: "StatRecords");

            migrationBuilder.DropColumn(name: "SignedOffByUserId", table: "StatRecords");
        }
    }
}
