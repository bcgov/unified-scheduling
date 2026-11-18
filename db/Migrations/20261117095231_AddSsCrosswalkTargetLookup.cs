using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddSsCrosswalkTargetLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationRecords_Source_TargetType_TargetKey",
                table: "DataMigrationRecords",
                columns: new[] { "Source", "TargetType", "TargetKey" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataMigrationRecords_Source_TargetType_TargetKey",
                table: "DataMigrationRecords"
            );
        }
    }
}
