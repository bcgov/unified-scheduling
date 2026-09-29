using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarConflictOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarConflictOverrides",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    FirstSourceModule = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    FirstEventId = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    SecondSourceModule = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    SecondEventId = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: false
                    ),
                    InvalidatedOn = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"
                    ),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarConflictOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarConflictOverrides_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_CalendarConflictOverrides_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CalendarConflictOverrides_CreatedById",
                table: "CalendarConflictOverrides",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CalendarConflictOverrides_FirstSourceModule_FirstEventId_Se~",
                table: "CalendarConflictOverrides",
                columns: new[]
                {
                    "FirstSourceModule",
                    "FirstEventId",
                    "SecondSourceModule",
                    "SecondEventId",
                    "ResourceId",
                },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CalendarConflictOverrides_UpdatedById",
                table: "CalendarConflictOverrides",
                column: "UpdatedById"
            );

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Group" },
                values: new object[]
                {
                    "CalendarConflictsOverride",
                    "Override calendar conflicts",
                    "Calendar",
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "CalendarConflictsOverride"
            );

            migrationBuilder.DropTable(name: "CalendarConflictOverrides");
        }
    }
}
