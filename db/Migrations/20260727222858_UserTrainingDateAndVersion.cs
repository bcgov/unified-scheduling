using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class UserTrainingDateAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndingOn",
                table: "UserTrainings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                )
            );

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "UserTrainings",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateIndex(
                name: "IX_UserTrainings_UserId_TrainingId_Version",
                table: "UserTrainings",
                columns: new[] { "UserId", "TrainingId", "Version" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTrainings_UserId_TrainingId_Version",
                table: "UserTrainings"
            );

            migrationBuilder.DropColumn(name: "EndingOn", table: "UserTrainings");

            migrationBuilder.DropColumn(name: "Version", table: "UserTrainings");
        }
    }
}
