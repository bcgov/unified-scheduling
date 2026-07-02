using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftSeries",
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
                    table.PrimaryKey("PK_ShiftSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftSeries_EventSeries_EventSeriesId",
                        column: x => x.EventSeriesId,
                        principalTable: "EventSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ShiftSeries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_ShiftSeries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ShiftEntries",
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
                    ShiftSeriesId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_ShiftEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftEntries_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ShiftEntries_ShiftSeries_ShiftSeriesId",
                        column: x => x.ShiftSeriesId,
                        principalTable: "ShiftSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_ShiftEntries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_ShiftEntries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ShiftSeriesUsers",
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
                    ShiftSeriesId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ShiftSeriesUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftSeriesUsers_ShiftSeries_ShiftSeriesId",
                        column: x => x.ShiftSeriesId,
                        principalTable: "ShiftSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ShiftSeriesUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_ShiftSeriesUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_ShiftSeriesUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ShiftEntryUsers",
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
                    ShiftEntryId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ShiftEntryUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftEntryUsers_ShiftEntries_ShiftEntryId",
                        column: x => x.ShiftEntryId,
                        principalTable: "ShiftEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ShiftEntryUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_ShiftEntryUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_ShiftEntryUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntries_CreatedById",
                table: "ShiftEntries",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntries_EventId",
                table: "ShiftEntries",
                column: "EventId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntries_ShiftSeriesId",
                table: "ShiftEntries",
                column: "ShiftSeriesId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntries_UpdatedById",
                table: "ShiftEntries",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntryUsers_CreatedById",
                table: "ShiftEntryUsers",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntryUsers_ShiftEntryId",
                table: "ShiftEntryUsers",
                column: "ShiftEntryId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntryUsers_ShiftEntryId_UserId",
                table: "ShiftEntryUsers",
                columns: new[] { "ShiftEntryId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntryUsers_UpdatedById",
                table: "ShiftEntryUsers",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntryUsers_UserId",
                table: "ShiftEntryUsers",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeries_CreatedById",
                table: "ShiftSeries",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeries_EventSeriesId",
                table: "ShiftSeries",
                column: "EventSeriesId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeries_UpdatedById",
                table: "ShiftSeries",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeriesUsers_CreatedById",
                table: "ShiftSeriesUsers",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeriesUsers_ShiftSeriesId",
                table: "ShiftSeriesUsers",
                column: "ShiftSeriesId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeriesUsers_ShiftSeriesId_UserId",
                table: "ShiftSeriesUsers",
                columns: new[] { "ShiftSeriesId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeriesUsers_UpdatedById",
                table: "ShiftSeriesUsers",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSeriesUsers_UserId",
                table: "ShiftSeriesUsers",
                column: "UserId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ShiftEntryUsers");

            migrationBuilder.DropTable(name: "ShiftSeriesUsers");

            migrationBuilder.DropTable(name: "ShiftEntries");

            migrationBuilder.DropTable(name: "ShiftSeries");
        }
    }
}
