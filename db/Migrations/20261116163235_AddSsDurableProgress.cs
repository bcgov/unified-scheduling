using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSsDurableProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommittedBatchCount",
                table: "DataMigrationRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HeartbeatOn",
                table: "DataMigrationRuns",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<DateOnly>(
                name: "HistoryStartDate",
                table: "DataMigrationRuns",
                type: "date",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "DataMigrationCheckpoints",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Source = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    Job = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    Stream = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
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
                    IdKind = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    SourceChangedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpperLegacyId = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    UpperChangedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommittedBatches = table.Column<long>(type: "bigint", nullable: false),
                    HistoryStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrationCheckpoints", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "DataMigrationWorkItems",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Source = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    Job = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    Stream = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
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
                    PendingRetry = table.Column<bool>(type: "boolean", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceChangedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrationWorkItems", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationCheckpoints_Source_Job_Stream_EntityType",
                table: "DataMigrationCheckpoints",
                columns: new[] { "Source", "Job", "Stream", "EntityType" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationWorkItems_Source_Job_Stream_EntityType_LegacyId",
                table: "DataMigrationWorkItems",
                columns: new[] { "Source", "Job", "Stream", "EntityType", "LegacyId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationWorkItems_Source_Job_Stream_PendingRetry",
                table: "DataMigrationWorkItems",
                columns: new[] { "Source", "Job", "Stream", "PendingRetry" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DataMigrationCheckpoints");

            migrationBuilder.DropTable(name: "DataMigrationWorkItems");

            migrationBuilder.DropColumn(name: "CommittedBatchCount", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "HeartbeatOn", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "HistoryStartDate", table: "DataMigrationRuns");
        }
    }
}
