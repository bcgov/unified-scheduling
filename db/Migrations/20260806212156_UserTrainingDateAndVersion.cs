using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class UserTrainingDateAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "Regions");

            migrationBuilder.RenameColumn(
                name: "JustinCode",
                table: "Locations",
                newName: "JustinLocationCode");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndingOn",
                table: "UserTrainings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "UserTrainings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "CourtRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Room = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtRooms_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CourtRooms_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourtRooms_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTrainings_UserId_TrainingId_Version",
                table: "UserTrainings",
                columns: new[] { "UserId", "TrainingId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourtRooms_CreatedById",
                table: "CourtRooms",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CourtRooms_LocationId",
                table: "CourtRooms",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtRooms_Room_LocationId",
                table: "CourtRooms",
                columns: new[] { "Room", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourtRooms_UpdatedById",
                table: "CourtRooms",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourtRooms");

            migrationBuilder.DropIndex(
                name: "IX_UserTrainings_UserId_TrainingId_Version",
                table: "UserTrainings");

            migrationBuilder.DropColumn(
                name: "EndingOn",
                table: "UserTrainings");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "UserTrainings");

            migrationBuilder.RenameColumn(
                name: "JustinLocationCode",
                table: "Locations",
                newName: "JustinCode");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Regions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
