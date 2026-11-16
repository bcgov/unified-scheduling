using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSsMigrationJobLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataMigrationRuns_Source_Status",
                table: "DataMigrationRuns"
            );

            migrationBuilder.AddColumn<DateOnly>(
                name: "CutoverDate",
                table: "DataMigrationRuns",
                type: "date",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Job",
                table: "DataMigrationRuns",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Legacy"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "MyTeamRunId",
                table: "DataMigrationRuns",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "AfterHash",
                table: "DataMigrationRecords",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByRunId",
                table: "DataMigrationRecords",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "DataMigrationOutcomes",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    LegacyId = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    Outcome = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    Reason = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrationOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataMigrationOutcomes_DataMigrationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "DataMigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationRuns_Source_Job",
                table: "DataMigrationRuns",
                columns: new[] { "Source", "Job" },
                unique: true,
                filter: "\"Status\" = 'Running'"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationOutcomes_RunId_EntityType_LegacyId",
                table: "DataMigrationOutcomes",
                columns: new[] { "RunId", "EntityType", "LegacyId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DataMigrationOutcomes");

            migrationBuilder.DropIndex(
                name: "IX_DataMigrationRuns_Source_Job",
                table: "DataMigrationRuns"
            );

            migrationBuilder.DropColumn(name: "CutoverDate", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "Job", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "MyTeamRunId", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "AfterHash", table: "DataMigrationRecords");

            migrationBuilder.DropColumn(name: "CreatedByRunId", table: "DataMigrationRecords");

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationRuns_Source_Status",
                table: "DataMigrationRuns",
                columns: new[] { "Source", "Status" },
                unique: true,
                filter: "\"Status\" = 'Running'"
            );
        }
    }
}
