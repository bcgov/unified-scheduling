using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanOutModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanOutSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventSeriesId = table.Column<int>(type: "integer", nullable: false),
                    LoanOutLocationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanOutSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanOutSeries_EventSeries_EventSeriesId",
                        column: x => x.EventSeriesId,
                        principalTable: "EventSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOutSeries_Locations_LoanOutLocationId",
                        column: x => x.LoanOutLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOutSeries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoanOutSeries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LoanOutEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    LoanOutLocationId = table.Column<int>(type: "integer", nullable: false),
                    LoanOutSeriesId = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanOutEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanOutEntries_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOutEntries_LoanOutSeries_LoanOutSeriesId",
                        column: x => x.LoanOutSeriesId,
                        principalTable: "LoanOutSeries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LoanOutEntries_Locations_LoanOutLocationId",
                        column: x => x.LoanOutLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanOutEntries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoanOutEntries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LoanOutSeriesUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanOutSeriesId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanOutSeriesUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanOutSeriesUsers_LoanOutSeries_LoanOutSeriesId",
                        column: x => x.LoanOutSeriesId,
                        principalTable: "LoanOutSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoanOutSeriesUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoanOutSeriesUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoanOutSeriesUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanOutEntryUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanOutEntryId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanOutEntryUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanOutEntryUsers_LoanOutEntries_LoanOutEntryId",
                        column: x => x.LoanOutEntryId,
                        principalTable: "LoanOutEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoanOutEntryUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoanOutEntryUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LoanOutEntryUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntries_CreatedById",
                table: "LoanOutEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntries_EventId",
                table: "LoanOutEntries",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntries_LoanOutLocationId",
                table: "LoanOutEntries",
                column: "LoanOutLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntries_LoanOutSeriesId",
                table: "LoanOutEntries",
                column: "LoanOutSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntries_UpdatedById",
                table: "LoanOutEntries",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntryUsers_CreatedById",
                table: "LoanOutEntryUsers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntryUsers_LoanOutEntryId",
                table: "LoanOutEntryUsers",
                column: "LoanOutEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntryUsers_LoanOutEntryId_UserId",
                table: "LoanOutEntryUsers",
                columns: new[] { "LoanOutEntryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntryUsers_UpdatedById",
                table: "LoanOutEntryUsers",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutEntryUsers_UserId",
                table: "LoanOutEntryUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeries_CreatedById",
                table: "LoanOutSeries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeries_EventSeriesId",
                table: "LoanOutSeries",
                column: "EventSeriesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeries_LoanOutLocationId",
                table: "LoanOutSeries",
                column: "LoanOutLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeries_UpdatedById",
                table: "LoanOutSeries",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeriesUsers_CreatedById",
                table: "LoanOutSeriesUsers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeriesUsers_LoanOutSeriesId",
                table: "LoanOutSeriesUsers",
                column: "LoanOutSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeriesUsers_LoanOutSeriesId_UserId",
                table: "LoanOutSeriesUsers",
                columns: new[] { "LoanOutSeriesId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeriesUsers_UpdatedById",
                table: "LoanOutSeriesUsers",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LoanOutSeriesUsers_UserId",
                table: "LoanOutSeriesUsers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanOutEntryUsers");

            migrationBuilder.DropTable(
                name: "LoanOutSeriesUsers");

            migrationBuilder.DropTable(
                name: "LoanOutEntries");

            migrationBuilder.DropTable(
                name: "LoanOutSeries");
        }
    }
}
