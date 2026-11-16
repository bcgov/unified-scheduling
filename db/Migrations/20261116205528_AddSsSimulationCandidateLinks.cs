using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSsSimulationCandidateLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataMigrationSimulationCandidates",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LegacyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrationSimulationCandidates", x => new { x.RunId, x.EntityType, x.LegacyId });
                    table.ForeignKey(
                        name: "FK_DataMigrationSimulationCandidates_DataMigrationSimulations_~",
                        column: x => x.RunId,
                        principalTable: "DataMigrationSimulations",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationSimulationCandidates_NamespaceId_EntityType_Le~",
                table: "DataMigrationSimulationCandidates",
                columns: new[] { "NamespaceId", "EntityType", "LegacyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataMigrationSimulationCandidates");
        }
    }
}
