using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Unified.Db.DataMigration;

namespace Unified.Db.Migrations;

[DbContext(typeof(UnifiedDbContext))]
[Migration("20261118000000_IndexSsJournalVersionLookup")]
public sealed class IndexSsJournalVersionLookup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(SsRollbackSchema.IndexedSubtransactionVersions);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(SsRollbackSchema.FixSubtransactionVersions);
}
