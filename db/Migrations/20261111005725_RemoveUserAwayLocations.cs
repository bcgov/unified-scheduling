using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserAwayLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN ('AwayLocationsView', 'AwayLocationsCreate', 'AwayLocationsEdit', 'AwayLocationsExpire');

                DELETE FROM "Permissions"
                WHERE "Id" IN ('AwayLocationsView', 'AwayLocationsCreate', 'AwayLocationsEdit', 'AwayLocationsExpire');
                """
            );

            migrationBuilder.DropTable(
                name: "UserAwayLocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAwayLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAwayLocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAwayLocations_CreatedById",
                table: "UserAwayLocations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserAwayLocations_EventId",
                table: "UserAwayLocations",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAwayLocations_UpdatedById",
                table: "UserAwayLocations",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserAwayLocations_UserId",
                table: "UserAwayLocations",
                column: "UserId");
        }
    }
}
