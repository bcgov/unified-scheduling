using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToStatRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "StatRecords",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_StatRecords_UserId",
                table: "StatRecords",
                column: "UserId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_StatRecords_Users_UserId",
                table: "StatRecords",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StatRecords_Users_UserId",
                table: "StatRecords"
            );

            migrationBuilder.DropIndex(name: "IX_StatRecords_UserId", table: "StatRecords");

            migrationBuilder.DropColumn(name: "UserId", table: "StatRecords");
        }
    }
}
