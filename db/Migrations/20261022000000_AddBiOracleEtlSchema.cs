using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddBiOracleEtlSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "etl");

            migrationBuilder.CreateTable(
                name: "BiOracleLocationMapping",
                schema: "etl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JustinLocationCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    CrtLocId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiOracleLocationMapping", x => x.Id);
                    table.CheckConstraint("CK_BiOracleLocationMapping_EffectivePeriod", "\"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_BiOracleLocationMapping_JustinLocationCode", "length(trim(\"JustinLocationCode\")) = 4");
                    table.ForeignKey(
                        name: "FK_BiOracleLocationMapping_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BiOracleLocationMapping_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BiOracleStatMappingSets",
                schema: "etl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiOracleStatMappingSets", x => x.Id);
                    table.CheckConstraint("CK_BiOracleStatMappingSets_EffectivePeriod", "\"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"EffectiveDate\"");
                    table.ForeignKey(
                        name: "FK_BiOracleStatMappingSets_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BiOracleStatMappingSets_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BiOracleEtlRuns",
                schema: "etl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportingMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    MappingSetId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceRecordCount = table.Column<int>(type: "integer", nullable: false),
                    StagedCellCount = table.Column<int>(type: "integer", nullable: false),
                    LoadedCellCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiOracleEtlRuns", x => x.Id);
                    table.UniqueConstraint("AK_BiOracleEtlRuns_LoadId", x => x.LoadId);
                    table.CheckConstraint("CK_BiOracleEtlRuns_Counts", "\"SourceRecordCount\" >= 0 AND \"StagedCellCount\" >= 0 AND \"LoadedCellCount\" >= 0");
                    table.CheckConstraint("CK_BiOracleEtlRuns_Status", "\"Status\" IN ('Running', 'Succeeded', 'Failed', 'Skipped')");
                    table.ForeignKey(
                        name: "FK_BiOracleEtlRuns_BiOracleStatMappingSets_MappingSetId",
                        column: x => x.MappingSetId,
                        principalSchema: "etl",
                        principalTable: "BiOracleStatMappingSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiOracleEtlRuns_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BiOracleEtlRuns_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BiOracleStatMappings",
                schema: "etl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MappingSetId = table.Column<int>(type: "integer", nullable: false),
                    SubCategoryMetricId = table.Column<int>(type: "integer", nullable: false),
                    TargetTable = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetColumn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiOracleStatMappings", x => x.Id);
                    table.CheckConstraint("CK_BiOracleStatMappings_TargetColumn", "length(trim(\"TargetColumn\")) > 0");
                    table.CheckConstraint("CK_BiOracleStatMappings_TargetTable", "\"TargetTable\" IN ('SHERIFF_SRVCES', 'SHERIFF_SRVCES_HOURS')");
                    table.ForeignKey(
                        name: "FK_BiOracleStatMappings_BiOracleStatMappingSets_MappingSetId",
                        column: x => x.MappingSetId,
                        principalSchema: "etl",
                        principalTable: "BiOracleStatMappingSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiOracleStatMappings_SubCategoryMetrics_SubCategoryMetricId",
                        column: x => x.SubCategoryMetricId,
                        principalTable: "SubCategoryMetrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiOracleStatMappings_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BiOracleStatMappings_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BiOracleStatStages",
                schema: "etl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportingMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    CrtLocId = table.Column<int>(type: "integer", nullable: false),
                    IsLocationLevel = table.Column<bool>(type: "boolean", nullable: false),
                    IsSupervisor = table.Column<bool>(type: "boolean", nullable: false),
                    TargetTable = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetColumn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StatValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SourceRowCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiOracleStatStages", x => x.Id);
                    table.CheckConstraint("CK_BiOracleStatStages_LocationLevelSupervisor", "NOT \"IsLocationLevel\" OR NOT \"IsSupervisor\"");
                    table.CheckConstraint("CK_BiOracleStatStages_SourceRowCount", "\"SourceRowCount\" > 0");
                    table.CheckConstraint("CK_BiOracleStatStages_TargetColumn", "length(trim(\"TargetColumn\")) > 0");
                    table.CheckConstraint("CK_BiOracleStatStages_TargetTable", "\"TargetTable\" IN ('SHERIFF_SRVCES', 'SHERIFF_SRVCES_HOURS')");
                    table.ForeignKey(
                        name: "FK_BiOracleStatStages_BiOracleEtlRuns_LoadId",
                        column: x => x.LoadId,
                        principalSchema: "etl",
                        principalTable: "BiOracleEtlRuns",
                        principalColumn: "LoadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleEtlRuns_CreatedById",
                schema: "etl",
                table: "BiOracleEtlRuns",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleEtlRuns_MappingSetId",
                schema: "etl",
                table: "BiOracleEtlRuns",
                column: "MappingSetId");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleEtlRuns_ReportingMonth",
                schema: "etl",
                table: "BiOracleEtlRuns",
                column: "ReportingMonth");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleEtlRuns_StartedAt",
                schema: "etl",
                table: "BiOracleEtlRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleEtlRuns_UpdatedById",
                schema: "etl",
                table: "BiOracleEtlRuns",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleLocationMapping_CreatedById",
                schema: "etl",
                table: "BiOracleLocationMapping",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleLocationMapping_CrtLocId",
                schema: "etl",
                table: "BiOracleLocationMapping",
                column: "CrtLocId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleLocationMapping_ExpiryDate",
                schema: "etl",
                table: "BiOracleLocationMapping",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleLocationMapping_JustinLocationCode",
                schema: "etl",
                table: "BiOracleLocationMapping",
                column: "JustinLocationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleLocationMapping_UpdatedById",
                schema: "etl",
                table: "BiOracleLocationMapping",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappings_CreatedById",
                schema: "etl",
                table: "BiOracleStatMappings",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappings_MappingSetId",
                schema: "etl",
                table: "BiOracleStatMappings",
                column: "MappingSetId");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappings_MappingSetId_SubCategoryMetricId_Targe~",
                schema: "etl",
                table: "BiOracleStatMappings",
                columns: new[] { "MappingSetId", "SubCategoryMetricId", "TargetTable", "TargetColumn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappings_SubCategoryMetricId",
                schema: "etl",
                table: "BiOracleStatMappings",
                column: "SubCategoryMetricId");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappings_UpdatedById",
                schema: "etl",
                table: "BiOracleStatMappings",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappingSets_CreatedById",
                schema: "etl",
                table: "BiOracleStatMappingSets",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappingSets_EffectiveDate",
                schema: "etl",
                table: "BiOracleStatMappingSets",
                column: "EffectiveDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappingSets_ExpiryDate",
                schema: "etl",
                table: "BiOracleStatMappingSets",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatMappingSets_UpdatedById",
                schema: "etl",
                table: "BiOracleStatMappingSets",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatStages_LoadId",
                schema: "etl",
                table: "BiOracleStatStages",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_BiOracleStatStages_LoadId_ReportingMonth_CrtLocId_IsLocatio~",
                schema: "etl",
                table: "BiOracleStatStages",
                columns: new[] { "LoadId", "ReportingMonth", "CrtLocId", "IsLocationLevel", "IsSupervisor", "TargetTable", "TargetColumn" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiOracleLocationMapping",
                schema: "etl");

            migrationBuilder.DropTable(
                name: "BiOracleStatMappings",
                schema: "etl");

            migrationBuilder.DropTable(
                name: "BiOracleStatStages",
                schema: "etl");

            migrationBuilder.DropTable(
                name: "BiOracleEtlRuns",
                schema: "etl");

            migrationBuilder.DropTable(
                name: "BiOracleStatMappingSets",
                schema: "etl");
        }
    }
}
