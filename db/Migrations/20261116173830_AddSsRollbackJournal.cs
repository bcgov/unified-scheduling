using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSsRollbackJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RollbackJournalKeyId",
                table: "DataMigrationRuns",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "DataMigrationJournals",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaName = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    TableName = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    RowKey = table.Column<string>(type: "jsonb", nullable: false),
                    Operation = table.Column<string>(
                        type: "character varying(1)",
                        maxLength: 1,
                        nullable: false
                    ),
                    BeforeImage = table.Column<byte[]>(type: "bytea", nullable: true),
                    AfterHash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    AfterVersion = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    ChangedColumns = table.Column<string[]>(type: "text[]", nullable: false),
                    BeforeHadPhoto = table.Column<bool>(type: "boolean", nullable: false),
                    PhotoChanged = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrationJournals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataMigrationJournals_DataMigrationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "DataMigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationJournals_RunId_Id",
                table: "DataMigrationJournals",
                columns: new[] { "RunId", "Id" }
            );
            migrationBuilder.Sql(DataMigration.SsRollbackSchema.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$ DECLARE t record;
                BEGIN
                  IF EXISTS (SELECT 1 FROM public."DataMigrationJournals") THEN
                    RAISE EXCEPTION 'Cannot remove retained SS rollback evidence';
                  END IF;
                  FOR t IN SELECT n.nspname,c.relname FROM pg_trigger g
                    JOIN pg_class c ON c.oid=g.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE g.tgname='ss_rollback_capture'
                  LOOP EXECUTE format('DROP TRIGGER ss_rollback_capture ON %I.%I',t.nspname,t.relname); END LOOP;
                END $$;
                DROP FUNCTION public.ss_capture_rollback();
                """
            );
            migrationBuilder.DropTable(name: "DataMigrationJournals");

            migrationBuilder.DropColumn(name: "RollbackJournalKeyId", table: "DataMigrationRuns");
            migrationBuilder.Sql("DROP FUNCTION public.ss_journal_immutable();");
        }
    }
}
