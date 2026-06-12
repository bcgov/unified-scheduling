using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventStatusTypes",
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
                    Code = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    Description = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    EffectiveDate = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ExpiryDate = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStatusTypes", x => x.Id);
                    table.UniqueConstraint("AK_EventStatusTypes_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_EventStatusTypes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_EventStatusTypes_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventTypes",
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
                    Code = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    Description = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    EffectiveDate = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ExpiryDate = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTypes", x => x.Id);
                    table.UniqueConstraint("AK_EventTypes_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_EventTypes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_EventTypes_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventSeries",
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
                    Title = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Description = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    Notes = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: true
                    ),
                    Color = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    RecurrenceRule = table.Column<string>(type: "text", nullable: true),
                    TimeZoneId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    StartAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    EventTypeCode = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false,
                        defaultValue: "general"
                    ),
                    StatusTypeCode = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false,
                        defaultValue: "draft"
                    ),
                    CancelledAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_EventSeries", x => x.Id);
                    table.CheckConstraint(
                        "CK_EventSeries_EndAfterStart",
                        "\"EndAtUtc\" IS NULL OR \"EndAtUtc\" > \"StartAtUtc\""
                    );
                    table.ForeignKey(
                        name: "FK_EventSeries_EventStatusTypes_StatusTypeCode",
                        column: x => x.StatusTypeCode,
                        principalTable: "EventStatusTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventSeries_EventTypes_EventTypeCode",
                        column: x => x.EventTypeCode,
                        principalTable: "EventTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventSeries_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventSeries_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_EventSeries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_EventSeries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Events",
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
                    EventSeriesId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Description = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    Notes = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: true
                    ),
                    Color = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    StartAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    SeriesStartAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    SeriesEndAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    TimeZoneId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    IsException = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    EventTypeCode = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false,
                        defaultValue: "general"
                    ),
                    StatusTypeCode = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false,
                        defaultValue: "draft"
                    ),
                    CancelledAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    SourceModule = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint(
                        "CK_Events_EndAfterStart",
                        "\"EndAtUtc\" IS NULL OR \"EndAtUtc\" > \"StartAtUtc\""
                    );
                    table.ForeignKey(
                        name: "FK_Events_EventSeries_EventSeriesId",
                        column: x => x.EventSeriesId,
                        principalTable: "EventSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Events_EventStatusTypes_StatusTypeCode",
                        column: x => x.StatusTypeCode,
                        principalTable: "EventStatusTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Events_EventTypes_EventTypeCode",
                        column: x => x.EventTypeCode,
                        principalTable: "EventTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Events_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Events_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Events_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Events_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_CancelledByUserId",
                table: "Events",
                column: "CancelledByUserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedById",
                table: "Events",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventSeriesId",
                table: "Events",
                column: "EventSeriesId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventTypeCode",
                table: "Events",
                column: "EventTypeCode"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_LocationId",
                table: "Events",
                column: "LocationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_SourceModule_StartAtUtc",
                table: "Events",
                columns: new[] { "SourceModule", "StartAtUtc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_StatusTypeCode",
                table: "Events",
                column: "StatusTypeCode"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_UpdatedById",
                table: "Events",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_CancelledByUserId",
                table: "EventSeries",
                column: "CancelledByUserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_CreatedById",
                table: "EventSeries",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_EventTypeCode",
                table: "EventSeries",
                column: "EventTypeCode"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_LocationId",
                table: "EventSeries",
                column: "LocationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_StartAtUtc",
                table: "EventSeries",
                column: "StartAtUtc"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_StatusTypeCode",
                table: "EventSeries",
                column: "StatusTypeCode"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_UpdatedById",
                table: "EventSeries",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventStatusTypes_Code",
                table: "EventStatusTypes",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventStatusTypes_CreatedById",
                table: "EventStatusTypes",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventStatusTypes_UpdatedById",
                table: "EventStatusTypes",
                column: "UpdatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTypes_Code",
                table: "EventTypes",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTypes_CreatedById",
                table: "EventTypes",
                column: "CreatedById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTypes_UpdatedById",
                table: "EventTypes",
                column: "UpdatedById"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Events");

            migrationBuilder.DropTable(name: "EventSeries");

            migrationBuilder.DropTable(name: "EventStatusTypes");

            migrationBuilder.DropTable(name: "EventTypes");
        }
    }
}
