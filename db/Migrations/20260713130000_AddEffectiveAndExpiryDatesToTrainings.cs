using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Unified.Db;

#nullable disable

namespace Unified.Db.Migrations
{
    [DbContext(typeof(UnifiedDbContext))]
    [Migration("20260713130000_AddEffectiveAndExpiryDatesToTrainings")]
    /// <inheritdoc />
    public partial class AddEffectiveAndExpiryDatesToTrainings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveDate",
                table: "Trainings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()"
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiryDate",
                table: "Trainings",
                type: "timestamp with time zone",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EffectiveDate", table: "Trainings");

            migrationBuilder.DropColumn(name: "ExpiryDate", table: "Trainings");
        }
    }
}
