using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddStatsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatGroups", x => x.Id);
                    table.ForeignKey(name: "FK_StatGroups_Users_CreatedById", column: x => x.CreatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_StatGroups_Users_UpdatedById", column: x => x.UpdatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                }
            );

            migrationBuilder.CreateTable(
                name: "StatMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "text", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatMetrics", x => x.Id);
                    table.ForeignKey(name: "FK_StatMetrics_Users_CreatedById", column: x => x.CreatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_StatMetrics_Users_UpdatedById", column: x => x.UpdatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                }
            );

            migrationBuilder.CreateTable(
                name: "StatCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsHighSecurity = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatCategories", x => x.Id);
                    table.ForeignKey(name: "FK_StatCategories_StatGroups_GroupId", column: x => x.GroupId, principalTable: "StatGroups", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_StatCategories_Users_CreatedById", column: x => x.CreatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_StatCategories_Users_UpdatedById", column: x => x.UpdatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                }
            );

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.Id);
                    table.ForeignKey(name: "FK_SubCategories_StatCategories_CategoryId", column: x => x.CategoryId, principalTable: "StatCategories", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_SubCategories_Users_CreatedById", column: x => x.CreatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_SubCategories_Users_UpdatedById", column: x => x.UpdatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                }
            );

            migrationBuilder.CreateTable(
                name: "SubCategoryMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubCategoryId = table.Column<int>(type: "integer", nullable: false),
                    MetricId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategoryMetrics", x => x.Id);
                    table.ForeignKey(name: "FK_SubCategoryMetrics_SubCategories_SubCategoryId", column: x => x.SubCategoryId, principalTable: "SubCategories", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_SubCategoryMetrics_StatMetrics_MetricId", column: x => x.MetricId, principalTable: "StatMetrics", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_SubCategoryMetrics_Users_CreatedById", column: x => x.CreatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_SubCategoryMetrics_Users_UpdatedById", column: x => x.UpdatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                }
            );

            migrationBuilder.CreateTable(
                name: "StatRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodType = table.Column<string>(type: "text", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    SubCategoryMetricId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatRecords", x => x.Id);
                    table.ForeignKey(name: "FK_StatRecords_SubCategoryMetrics_SubCategoryMetricId", column: x => x.SubCategoryMetricId, principalTable: "SubCategoryMetrics", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_StatRecords_Locations_LocationId", column: x => x.LocationId, principalTable: "Locations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_StatRecords_Users_CreatedById", column: x => x.CreatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_StatRecords_Users_UpdatedById", column: x => x.UpdatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                }
            );

            migrationBuilder.CreateTable(
                name: "StatSignoffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    SignoffDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatSignoffs", x => x.Id);
                    table.ForeignKey(name: "FK_StatSignoffs_Locations_LocationId", column: x => x.LocationId, principalTable: "Locations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_StatSignoffs_Users_CreatedById", column: x => x.CreatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_StatSignoffs_Users_UpdatedById", column: x => x.UpdatedById, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(name: "FK_StatSignoffs_Users_UserId", column: x => x.UserId, principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                }
            );

            // StatGroups indexes
            migrationBuilder.CreateIndex(name: "IX_StatGroups_CreatedById", table: "StatGroups", column: "CreatedById");
            migrationBuilder.CreateIndex(name: "IX_StatGroups_UpdatedById", table: "StatGroups", column: "UpdatedById");

            // StatMetrics indexes
            migrationBuilder.CreateIndex(name: "IX_StatMetrics_CreatedById", table: "StatMetrics", column: "CreatedById");
            migrationBuilder.CreateIndex(name: "IX_StatMetrics_UpdatedById", table: "StatMetrics", column: "UpdatedById");

            // StatCategories indexes
            migrationBuilder.CreateIndex(name: "IX_StatCategories_GroupId", table: "StatCategories", column: "GroupId");
            migrationBuilder.CreateIndex(name: "IX_StatCategories_CreatedById", table: "StatCategories", column: "CreatedById");
            migrationBuilder.CreateIndex(name: "IX_StatCategories_UpdatedById", table: "StatCategories", column: "UpdatedById");

            // SubCategories indexes
            migrationBuilder.CreateIndex(name: "IX_SubCategories_CategoryId", table: "SubCategories", column: "CategoryId");
            migrationBuilder.CreateIndex(name: "IX_SubCategories_CreatedById", table: "SubCategories", column: "CreatedById");
            migrationBuilder.CreateIndex(name: "IX_SubCategories_UpdatedById", table: "SubCategories", column: "UpdatedById");

            // SubCategoryMetrics indexes
            migrationBuilder.CreateIndex(name: "IX_SubCategoryMetrics_SubCategoryId", table: "SubCategoryMetrics", column: "SubCategoryId");
            migrationBuilder.CreateIndex(name: "IX_SubCategoryMetrics_MetricId", table: "SubCategoryMetrics", column: "MetricId");
            migrationBuilder.CreateIndex(name: "IX_SubCategoryMetrics_CreatedById", table: "SubCategoryMetrics", column: "CreatedById");
            migrationBuilder.CreateIndex(name: "IX_SubCategoryMetrics_UpdatedById", table: "SubCategoryMetrics", column: "UpdatedById");

            // StatRecords indexes
            migrationBuilder.CreateIndex(name: "IX_StatRecords_LocationId", table: "StatRecords", column: "LocationId");
            migrationBuilder.CreateIndex(name: "IX_StatRecords_SubCategoryMetricId", table: "StatRecords", column: "SubCategoryMetricId");
            migrationBuilder.CreateIndex(name: "IX_StatRecords_DateFrom_DateTo", table: "StatRecords", columns: new[] { "DateFrom", "DateTo" });
            migrationBuilder.CreateIndex(name: "IX_StatRecords_CreatedById", table: "StatRecords", column: "CreatedById");
            migrationBuilder.CreateIndex(name: "IX_StatRecords_UpdatedById", table: "StatRecords", column: "UpdatedById");

            // StatSignoffs indexes
            migrationBuilder.CreateIndex(name: "IX_StatSignoffs_UserId_LocationId_Month_Year", table: "StatSignoffs", columns: new[] { "UserId", "LocationId", "Month", "Year" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_StatSignoffs_LocationId", table: "StatSignoffs", column: "LocationId");
            migrationBuilder.CreateIndex(name: "IX_StatSignoffs_UserId", table: "StatSignoffs", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_StatSignoffs_CreatedById", table: "StatSignoffs", column: "CreatedById");
            migrationBuilder.CreateIndex(name: "IX_StatSignoffs_UpdatedById", table: "StatSignoffs", column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StatRecords");
            migrationBuilder.DropTable(name: "StatSignoffs");
            migrationBuilder.DropTable(name: "SubCategoryMetrics");
            migrationBuilder.DropTable(name: "SubCategories");
            migrationBuilder.DropTable(name: "StatCategories");
            migrationBuilder.DropTable(name: "StatMetrics");
            migrationBuilder.DropTable(name: "StatGroups");
        }
    }
}
