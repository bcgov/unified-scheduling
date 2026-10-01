using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations;

public partial class AddDataMigrationState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DataMigrationControls",
            columns: table => new
            {
                Source = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                AbortRequestedOn = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                ChangedOn = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                ChangedBy = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true
                ),
            },
            constraints: table => table.PrimaryKey("PK_DataMigrationControls", x => x.Source)
        );

        migrationBuilder.CreateTable(
            name: "DataMigrationRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                Status = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                StartedOn = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                CompletedOn = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                WatermarkStartedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                WatermarkCompletedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                DryRun = table.Column<bool>(type: "boolean", nullable: false),
                ScannedCount = table.Column<int>(type: "integer", nullable: false),
                InsertedCount = table.Column<int>(type: "integer", nullable: false),
                UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                UnchangedCount = table.Column<int>(type: "integer", nullable: false),
                FailedCount = table.Column<int>(type: "integer", nullable: false),
                SkippedCount = table.Column<int>(type: "integer", nullable: false),
                RejectedCount = table.Column<int>(type: "integer", nullable: false),
                RollbackOfRunId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataMigrationRuns", x => x.Id);
                table.ForeignKey(
                    "FK_DataMigrationRuns_DataMigrationRuns_RollbackOfRunId",
                    x => x.RollbackOfRunId,
                    "DataMigrationRuns",
                    "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "DataMigrationRecords",
            columns: table => new
            {
                Id = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        Npgsql
                            .EntityFrameworkCore
                            .PostgreSQL
                            .Metadata
                            .NpgsqlValueGenerationStrategy
                            .IdentityByDefaultColumn
                    ),
                Source = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
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
                TargetType = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false
                ),
                TargetKey = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false
                ),
                SourceUpdatedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                PayloadHash = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false
                ),
                LastAppliedRunId = table.Column<Guid>(type: "uuid", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataMigrationRecords", x => x.Id);
                table.ForeignKey(
                    "FK_DataMigrationRecords_DataMigrationRuns_LastAppliedRunId",
                    x => x.LastAppliedRunId,
                    "DataMigrationRuns",
                    "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "DataMigrationWatermarks",
            columns: table => new
            {
                Id = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        Npgsql
                            .EntityFrameworkCore
                            .PostgreSQL
                            .Metadata
                            .NpgsqlValueGenerationStrategy
                            .IdentityByDefaultColumn
                    ),
                Source = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                EntityType = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false
                ),
                SourceChangedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                LegacyId = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false
                ),
                LastCompletedRunId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataMigrationWatermarks", x => x.Id);
                table.ForeignKey(
                    "FK_DataMigrationWatermarks_DataMigrationRuns_LastCompletedRunId",
                    x => x.LastCompletedRunId,
                    "DataMigrationRuns",
                    "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "DataMigrationFailures",
            columns: table => new
            {
                Id = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        Npgsql
                            .EntityFrameworkCore
                            .PostgreSQL
                            .Metadata
                            .NpgsqlValueGenerationStrategy
                            .IdentityByDefaultColumn
                    ),
                RunId = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                EntityType = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true
                ),
                LegacyId = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true
                ),
                ErrorType = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false
                ),
                ErrorMessage = table.Column<string>(
                    type: "character varying(2048)",
                    maxLength: 2048,
                    nullable: false
                ),
                Watermark = table.Column<string>(
                    type: "character varying(512)",
                    maxLength: 512,
                    nullable: true
                ),
                OccurredOn = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataMigrationFailures", x => x.Id);
                table.ForeignKey(
                    "FK_DataMigrationFailures_DataMigrationRuns_RunId",
                    x => x.RunId,
                    "DataMigrationRuns",
                    "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "DataMigrationMutations",
            columns: table => new
            {
                Id = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        Npgsql
                            .EntityFrameworkCore
                            .PostgreSQL
                            .Metadata
                            .NpgsqlValueGenerationStrategy
                            .IdentityByDefaultColumn
                    ),
                RunId = table.Column<Guid>(type: "uuid", nullable: false),
                DataMigrationRecordId = table.Column<long>(type: "bigint", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false),
                Operation = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                BeforeImage = table.Column<string>(type: "text", nullable: true),
                AfterHash = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false
                ),
                Version = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataMigrationMutations", x => x.Id);
                table.ForeignKey(
                    "FK_DataMigrationMutations_DataMigrationRecords_DataMigrationRecordId",
                    x => x.DataMigrationRecordId,
                    "DataMigrationRecords",
                    "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    "FK_DataMigrationMutations_DataMigrationRuns_RunId",
                    x => x.RunId,
                    "DataMigrationRuns",
                    "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationFailures_RunId_OccurredOn",
            table: "DataMigrationFailures",
            columns: new[] { "RunId", "OccurredOn" },
            descending: new[] { false, true }
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationMutations_DataMigrationRecordId",
            table: "DataMigrationMutations",
            column: "DataMigrationRecordId"
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationMutations_RunId_Sequence",
            table: "DataMigrationMutations",
            columns: new[] { "RunId", "Sequence" },
            descending: new[] { false, true }
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationRecords_LastAppliedRunId",
            table: "DataMigrationRecords",
            column: "LastAppliedRunId"
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationRecords_Source_EntityType_LegacyId",
            table: "DataMigrationRecords",
            columns: new[] { "Source", "EntityType", "LegacyId" },
            unique: true
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationRuns_RollbackOfRunId",
            table: "DataMigrationRuns",
            column: "RollbackOfRunId"
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationRuns_Source_Status",
            table: "DataMigrationRuns",
            columns: new[] { "Source", "Status" },
            unique: true,
            filter: "\"Status\" = 'Running'"
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationWatermarks_LastCompletedRunId",
            table: "DataMigrationWatermarks",
            column: "LastCompletedRunId"
        );
        migrationBuilder.CreateIndex(
            name: "IX_DataMigrationWatermarks_Source_EntityType",
            table: "DataMigrationWatermarks",
            columns: new[] { "Source", "EntityType" },
            unique: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DataMigrationControls");
        migrationBuilder.DropTable(name: "DataMigrationFailures");
        migrationBuilder.DropTable(name: "DataMigrationMutations");
        migrationBuilder.DropTable(name: "DataMigrationWatermarks");
        migrationBuilder.DropTable(name: "DataMigrationRecords");
        migrationBuilder.DropTable(name: "DataMigrationRuns");
    }
}
