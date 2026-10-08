using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeOffModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("UserLeaves");
            
            migrationBuilder.CreateTable(
                name: "TimeOffSeries",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:IdentitySequenceOptions",
                            "'200', '1', '', '', 'False', '1'"
                        )
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    EventSeriesId = table.Column<int>(type: "integer", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_TimeOffSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeOffSeries_EventSeries_EventSeriesId",
                        column: x => x.EventSeriesId,
                        principalTable: "EventSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffSeries_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffSeries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffSeries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TimeOffEntries",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:IdentitySequenceOptions",
                            "'200', '1', '', '', 'False', '1'"
                        )
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    TimeOffSeriesId = table.Column<int>(type: "integer", nullable: true),
                    LeaveTypeId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_TimeOffEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeOffEntries_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffEntries_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffEntries_TimeOffSeries_TimeOffSeriesId",
                        column: x => x.TimeOffSeriesId,
                        principalTable: "TimeOffSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffEntries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffEntries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TimeOffSeriesUsers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:IdentitySequenceOptions",
                            "'200', '1', '', '', 'False', '1'"
                        )
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    TimeOffSeriesId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_TimeOffSeriesUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeOffSeriesUsers_TimeOffSeries_TimeOffSeriesId",
                        column: x => x.TimeOffSeriesId,
                        principalTable: "TimeOffSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffSeriesUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffSeriesUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffSeriesUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TimeOffEntryUsers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:IdentitySequenceOptions",
                            "'200', '1', '', '', 'False', '1'"
                        )
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    TimeOffEntryId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_TimeOffEntryUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeOffEntryUsers_TimeOffEntries_TimeOffEntryId",
                        column: x => x.TimeOffEntryId,
                        principalTable: "TimeOffEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffEntryUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffEntryUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_TimeOffEntryUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntries_CreatedById",
                table: "TimeOffEntries",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntries_EventId",
                table: "TimeOffEntries",
                column: "EventId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntries_LeaveTypeId",
                table: "TimeOffEntries",
                column: "LeaveTypeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntries_TimeOffSeriesId",
                table: "TimeOffEntries",
                column: "TimeOffSeriesId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntries_UpdatedById",
                table: "TimeOffEntries",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntryUsers_CreatedById",
                table: "TimeOffEntryUsers",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntryUsers_TimeOffEntryId",
                table: "TimeOffEntryUsers",
                column: "TimeOffEntryId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntryUsers_TimeOffEntryId_UserId",
                table: "TimeOffEntryUsers",
                columns: new[] { "TimeOffEntryId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntryUsers_UpdatedById",
                table: "TimeOffEntryUsers",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffEntryUsers_UserId",
                table: "TimeOffEntryUsers",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeries_CreatedById",
                table: "TimeOffSeries",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeries_EventSeriesId",
                table: "TimeOffSeries",
                column: "EventSeriesId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeries_LeaveTypeId",
                table: "TimeOffSeries",
                column: "LeaveTypeId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeries_UpdatedById",
                table: "TimeOffSeries",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeriesUsers_CreatedById",
                table: "TimeOffSeriesUsers",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeriesUsers_TimeOffSeriesId",
                table: "TimeOffSeriesUsers",
                column: "TimeOffSeriesId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeriesUsers_TimeOffSeriesId_UserId",
                table: "TimeOffSeriesUsers",
                columns: new[] { "TimeOffSeriesId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeriesUsers_UpdatedById",
                table: "TimeOffSeriesUsers",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffSeriesUsers_UserId",
                table: "TimeOffSeriesUsers",
                column: "UserId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimeOffEntryUsers");

            migrationBuilder.DropTable(
                name: "TimeOffSeriesUsers");

            migrationBuilder.DropTable(
                name: "TimeOffEntries");

            migrationBuilder.DropTable(
                name: "TimeOffSeries");

        }
    }
}
