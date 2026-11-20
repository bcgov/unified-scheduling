using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSsRunAuditSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuditSourceFingerprint",
                table: "DataMigrationRuns",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "AuditSourceRowCount",
                table: "DataMigrationRuns",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatedByUserId",
                table: "DataMigrationRuns",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "InitiationKind",
                table: "DataMigrationRuns",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PersistenceMode",
                table: "DataMigrationRuns",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ux_audit_migration_run_summary",
                table: "AuditRecords",
                columns: new[] { "EntityType", "EntityPK" },
                unique: true,
                filter: "\"EntityType\" = 'DataMigrationRunSummary'"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_audit_migration_run_summary",
                table: "AuditRecords"
            );

            migrationBuilder.DropColumn(name: "AuditSourceFingerprint", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "AuditSourceRowCount", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "InitiatedByUserId", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "InitiationKind", table: "DataMigrationRuns");

            migrationBuilder.DropColumn(name: "PersistenceMode", table: "DataMigrationRuns");
        }
    }
}
