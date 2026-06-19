using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteForRolesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "Roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedOn",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_HomeLocationId",
                table: "Users",
                column: "HomeLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Locations_HomeLocationId",
                table: "Users",
                column: "HomeLocationId",
                principalTable: "Locations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Locations_HomeLocationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_HomeLocationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Roles");
        }
    }
}
