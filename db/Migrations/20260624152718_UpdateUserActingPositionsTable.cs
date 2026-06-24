using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserActingPositionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExpiryDate",
                table: "UserActingPositions",
                newName: "ExpiryAtUtc");

            migrationBuilder.RenameColumn(
                name: "EffectiveDate",
                table: "UserActingPositions",
                newName: "StartAtUtc");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndAtUtc",
                table: "UserActingPositions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndAtUtc",
                table: "UserActingPositions");

            migrationBuilder.RenameColumn(
                name: "StartAtUtc",
                table: "UserActingPositions",
                newName: "EffectiveDate");

            migrationBuilder.RenameColumn(
                name: "ExpiryAtUtc",
                table: "UserActingPositions",
                newName: "ExpiryDate");
        }
    }
}
