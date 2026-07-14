using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserActingPositionEndDateColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndAtUtc",
                table: "UserActingPositions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndAtUtc",
                table: "UserActingPositions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone"
            );
        }
    }
}
