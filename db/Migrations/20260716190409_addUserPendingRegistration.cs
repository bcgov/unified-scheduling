using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class addUserPendingRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PendingRegistration",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdirId",
                table: "Users",
                column: "IdirId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdirName",
                table: "Users",
                column: "IdirName",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_KeyCloakId",
                table: "Users",
                column: "KeyCloakId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Users_IdirId", table: "Users");

            migrationBuilder.DropIndex(name: "IX_Users_IdirName", table: "Users");

            migrationBuilder.DropIndex(name: "IX_Users_KeyCloakId", table: "Users");

            migrationBuilder.DropColumn(name: "PendingRegistration", table: "Users");
        }
    }
}
