using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSsSimulationCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataMigrationSimulations",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MyTeamRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceHash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    BaselineHash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    ConfigurationHash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    CandidateHash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    FormatVersion = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrationSimulations", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_DataMigrationSimulations_DataMigrationRuns_MyTeamRunId",
                        column: x => x.MyTeamRunId,
                        principalTable: "DataMigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_DataMigrationSimulations_DataMigrationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "DataMigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationSimulations_MyTeamRunId",
                table: "DataMigrationSimulations",
                column: "MyTeamRunId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationSimulations_NamespaceId_MyTeamRunId",
                table: "DataMigrationSimulations",
                columns: new[] { "NamespaceId", "MyTeamRunId" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DataMigrationSimulations");
        }
    }
}
