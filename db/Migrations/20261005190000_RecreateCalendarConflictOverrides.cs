using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Unified.Db;

#nullable disable

namespace Unified.Db.Migrations
{
    [DbContext(typeof(UnifiedDbContext))]
    [Migration("20261005190000_RecreateCalendarConflictOverrides")]
    public partial class RecreateCalendarConflictOverrides : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CalendarConflictOverrides");

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CalendarConflictOverrides");

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
                    FirstEventId = table.Column<int>(type: "integer", nullable: false),
                    SecondEventId = table.Column<int>(type: "integer", nullable: false),
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
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.CheckConstraint(
                        "CK_CalendarConflictOverrides_NormalizedPair",
                        "\"FirstEventId\" < \"SecondEventId\""
                    );
                    table.ForeignKey(
                        name: "FK_CalendarConflictOverrides_Events_FirstEventId",
                        column: x => x.FirstEventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CalendarConflictOverrides_Events_SecondEventId",
                        column: x => x.SecondEventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CalendarConflictOverrides_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
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
                name: "IX_CalendarConflictOverrides_FirstEventId_SecondEventId_Resour~",
                table: "CalendarConflictOverrides",
                columns: new[] { "FirstEventId", "SecondEventId", "ResourceId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CalendarConflictOverrides_SecondEventId",
                table: "CalendarConflictOverrides",
                column: "SecondEventId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CalendarConflictOverrides_UpdatedById",
                table: "CalendarConflictOverrides",
                column: "UpdatedById"
            );
        }
    }
}
