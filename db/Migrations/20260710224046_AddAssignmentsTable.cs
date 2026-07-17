using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndAtUtc",
                table: "UserActingPositions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AssignmentCategoryTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EffectiveDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentCategoryTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentCategoryTypes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentCategoryTypes_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentSubCategoryTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EffectiveDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AssignmentCategoryTypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentSubCategoryTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentSubCategoryTypes_AssignmentCategoryTypes_Assignme~",
                        column: x => x.AssignmentCategoryTypeId,
                        principalTable: "AssignmentCategoryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentSubCategoryTypes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentSubCategoryTypes_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssignmentCategoryTypeId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentSubCategoryTypeId = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DefaultEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DefaultCapacity = table.Column<int>(type: "integer", nullable: false),
                    EffectiveDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiryDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentDefinitions", x => x.Id);
                    table.CheckConstraint("CK_AssignmentDefinitions_DefaultCapacityAtLeastOne", "\"DefaultCapacity\" >= 1");
                    table.CheckConstraint("CK_AssignmentDefinitions_DefaultEndAfterStart", "\"DefaultStartTime\" IS NULL OR \"DefaultEndTime\" IS NULL OR \"DefaultEndTime\" > \"DefaultStartTime\"");
                    table.CheckConstraint("CK_AssignmentDefinitions_ExpiryAfterEffective", "\"ExpiryDateUtc\" IS NULL OR \"ExpiryDateUtc\" > \"EffectiveDateUtc\"");
                    table.ForeignKey(
                        name: "FK_AssignmentDefinitions_AssignmentCategoryTypes_AssignmentCat~",
                        column: x => x.AssignmentCategoryTypeId,
                        principalTable: "AssignmentCategoryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentDefinitions_AssignmentSubCategoryTypes_Assignment~",
                        column: x => x.AssignmentSubCategoryTypeId,
                        principalTable: "AssignmentSubCategoryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentDefinitions_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentDefinitions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentDefinitions_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventSeriesId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentSeries", x => x.Id);
                    table.CheckConstraint("CK_AssignmentSeries_CapacityAtLeastOne", "\"Capacity\" >= 1");
                    table.ForeignKey(
                        name: "FK_AssignmentSeries_AssignmentDefinitions_AssignmentDefinition~",
                        column: x => x.AssignmentDefinitionId,
                        principalTable: "AssignmentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentSeries_EventSeries_EventSeriesId",
                        column: x => x.EventSeriesId,
                        principalTable: "EventSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentSeries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentSeries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignmentSeriesId = table.Column<int>(type: "integer", nullable: true),
                    AssignmentDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentEntries", x => x.Id);
                    table.CheckConstraint("CK_AssignmentEntries_CapacityAtLeastOne", "\"Capacity\" >= 1");
                    table.ForeignKey(
                        name: "FK_AssignmentEntries_AssignmentDefinitions_AssignmentDefinitio~",
                        column: x => x.AssignmentDefinitionId,
                        principalTable: "AssignmentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentEntries_AssignmentSeries_AssignmentSeriesId",
                        column: x => x.AssignmentSeriesId,
                        principalTable: "AssignmentSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentEntries_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentEntries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentEntries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignmentSeriesLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftSeriesId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentSeriesId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignmentSeriesLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinks_AssignmentSeries_AssignmentSeri~",
                        column: x => x.AssignmentSeriesId,
                        principalTable: "AssignmentSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinks_ShiftSeries_ShiftSeriesId",
                        column: x => x.ShiftSeriesId,
                        principalTable: "ShiftSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinks_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinks_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignmentEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftEntryId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentEntryId = table.Column<int>(type: "integer", nullable: false),
                    ShiftAssignmentSeriesLinkId = table.Column<int>(type: "integer", nullable: true),
                    IsException = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignmentEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntries_AssignmentEntries_AssignmentEntryId",
                        column: x => x.AssignmentEntryId,
                        principalTable: "AssignmentEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntries_ShiftEntries_ShiftEntryId",
                        column: x => x.ShiftEntryId,
                        principalTable: "ShiftEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntries_ShiftAssignmentSeriesLinks_ShiftAs~",
                        column: x => x.ShiftAssignmentSeriesLinkId,
                        principalTable: "ShiftAssignmentSeriesLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntries_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignmentEntryUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftAssignmentEntryId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignmentEntryUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntryUsers_ShiftAssignmentEntries_ShiftAssig~",
                        column: x => x.ShiftAssignmentEntryId,
                        principalTable: "ShiftAssignmentEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntryUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntryUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentEntryUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignmentSeriesLinkUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShiftAssignmentSeriesLinkId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignmentSeriesLinkUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinkUsers_ShiftAssignmentSeriesLinks_~",
                        column: x => x.ShiftAssignmentSeriesLinkId,
                        principalTable: "ShiftAssignmentSeriesLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinkUsers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinkUsers_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShiftAssignmentSeriesLinkUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventSeriesId_SeriesStartAtUtc_SeriesEndAtUtc",
                table: "Events",
                columns: new[] { "EventSeriesId", "SeriesStartAtUtc", "SeriesEndAtUtc" },
                unique: true,
                filter: "\"EventSeriesId\" IS NOT NULL AND \"SeriesStartAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCategoryTypes_Code",
                table: "AssignmentCategoryTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCategoryTypes_CreatedById",
                table: "AssignmentCategoryTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCategoryTypes_UpdatedById",
                table: "AssignmentCategoryTypes",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDefinitions_AssignmentCategoryTypeId",
                table: "AssignmentDefinitions",
                column: "AssignmentCategoryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDefinitions_AssignmentSubCategoryTypeId",
                table: "AssignmentDefinitions",
                column: "AssignmentSubCategoryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDefinitions_LocationId_Name",
                table: "AssignmentDefinitions",
                columns: new[] { "LocationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDefinitions_CreatedById",
                table: "AssignmentDefinitions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDefinitions_EffectiveDateUtc_ExpiryDateUtc",
                table: "AssignmentDefinitions",
                columns: new[] { "EffectiveDateUtc", "ExpiryDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDefinitions_UpdatedById",
                table: "AssignmentDefinitions",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEntries_AssignmentDefinitionId",
                table: "AssignmentEntries",
                column: "AssignmentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEntries_AssignmentSeriesId",
                table: "AssignmentEntries",
                column: "AssignmentSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEntries_CreatedById",
                table: "AssignmentEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEntries_EventId",
                table: "AssignmentEntries",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentEntries_UpdatedById",
                table: "AssignmentEntries",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSeries_AssignmentDefinitionId",
                table: "AssignmentSeries",
                column: "AssignmentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSeries_CreatedById",
                table: "AssignmentSeries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSeries_EventSeriesId",
                table: "AssignmentSeries",
                column: "EventSeriesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSeries_UpdatedById",
                table: "AssignmentSeries",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubCategoryTypes_AssignmentCategoryTypeId",
                table: "AssignmentSubCategoryTypes",
                column: "AssignmentCategoryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubCategoryTypes_AssignmentCategoryTypeId_Code",
                table: "AssignmentSubCategoryTypes",
                columns: new[] { "AssignmentCategoryTypeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubCategoryTypes_CreatedById",
                table: "AssignmentSubCategoryTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubCategoryTypes_UpdatedById",
                table: "AssignmentSubCategoryTypes",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntries_AssignmentEntryId",
                table: "ShiftAssignmentEntries",
                column: "AssignmentEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntries_CreatedById",
                table: "ShiftAssignmentEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntries_ShiftEntryId",
                table: "ShiftAssignmentEntries",
                column: "ShiftEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntries_ShiftAssignmentSeriesLinkId",
                table: "ShiftAssignmentEntries",
                column: "ShiftAssignmentSeriesLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntries_ShiftEntryId_AssignmentEntryId",
                table: "ShiftAssignmentEntries",
                columns: new[] { "ShiftEntryId", "AssignmentEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntries_UpdatedById",
                table: "ShiftAssignmentEntries",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntryUsers_CreatedById",
                table: "ShiftAssignmentEntryUsers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntryUsers_ShiftAssignmentEntryId",
                table: "ShiftAssignmentEntryUsers",
                column: "ShiftAssignmentEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntryUsers_ShiftAssignmentEntryId_UserId",
                table: "ShiftAssignmentEntryUsers",
                columns: new[] { "ShiftAssignmentEntryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntryUsers_UpdatedById",
                table: "ShiftAssignmentEntryUsers",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentEntryUsers_UserId",
                table: "ShiftAssignmentEntryUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinks_AssignmentSeriesId",
                table: "ShiftAssignmentSeriesLinks",
                column: "AssignmentSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinks_CreatedById",
                table: "ShiftAssignmentSeriesLinks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinks_ShiftSeriesId",
                table: "ShiftAssignmentSeriesLinks",
                column: "ShiftSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinks_ShiftSeriesId_AssignmentSerie~",
                table: "ShiftAssignmentSeriesLinks",
                columns: new[] { "ShiftSeriesId", "AssignmentSeriesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinks_UpdatedById",
                table: "ShiftAssignmentSeriesLinks",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinkUsers_CreatedById",
                table: "ShiftAssignmentSeriesLinkUsers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinkUsers_ShiftAssignmentSeriesLinkId",
                table: "ShiftAssignmentSeriesLinkUsers",
                column: "ShiftAssignmentSeriesLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinkUsers_ShiftAssignmentSeriesLinkI~",
                table: "ShiftAssignmentSeriesLinkUsers",
                columns: new[] { "ShiftAssignmentSeriesLinkId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinkUsers_UpdatedById",
                table: "ShiftAssignmentSeriesLinkUsers",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignmentSeriesLinkUsers_UserId",
                table: "ShiftAssignmentSeriesLinkUsers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftAssignmentEntryUsers");

            migrationBuilder.DropTable(
                name: "ShiftAssignmentSeriesLinkUsers");

            migrationBuilder.DropTable(
                name: "ShiftAssignmentEntries");

            migrationBuilder.DropTable(
                name: "ShiftAssignmentSeriesLinks");

            migrationBuilder.DropTable(
                name: "AssignmentEntries");

            migrationBuilder.DropTable(
                name: "AssignmentSeries");

            migrationBuilder.DropTable(
                name: "AssignmentDefinitions");

            migrationBuilder.DropTable(
                name: "AssignmentSubCategoryTypes");

            migrationBuilder.DropTable(
                name: "AssignmentCategoryTypes");

            migrationBuilder.DropIndex(
                name: "IX_Events_EventSeriesId_SeriesStartAtUtc_SeriesEndAtUtc",
                table: "Events");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndAtUtc",
                table: "UserActingPositions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }
    }
}
