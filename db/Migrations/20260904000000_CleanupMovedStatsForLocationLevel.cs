using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Unified.Db;

#nullable disable

namespace Unified.Db.Migrations;

[DbContext(typeof(UnifiedDbContext))]
[Migration("20260904000000_CleanupMovedStatsForLocationLevel")]
public partial class CleanupMovedStatsForLocationLevel : Migration
{
    /// <summary>
    /// Removes StatRecord rows that reference SubCategoryMetric IDs being moved from
    /// employee-level forms (Non-Supervision / Supervision) to the new Location Level
    /// form (GroupId 3). This must run before the seeder deletes those SubCategoryMetric
    /// rows, since the FK has Restrict delete behaviour.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM "StatRecords"
            WHERE "SubCategoryMetricId" IN (
                -- Circuit travel NS (SubCat 18) -> km Travelled
                71,
                -- Coroner Jury Admin NS (SubCat 19) -> Jurors Summonsed, Panels Created
                73, 74,
                -- Criminal/Civil Jury Admin NS (SubCat 20) -> Summonsed, Paid, Panels, $
                76, 77, 78, 79,
                -- Escorts Air NS (SubCat 29) -> L1/L2/L3 Trips
                118, 119, 120,
                -- Escorts Ground NS (SubCat 30) -> Trips, km, ground counts
                130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140,
                -- Holding NS SubCat 37-43 -> Cell Block Hours, Regulars, SEG
                165, 167, 168,
                169, 171, 172,
                173, 175, 176,
                177, 179, 180,
                181,
                183, 185,
                186, 188,
                -- Circuit travel SUP (SubCat 70) -> km Travelled
                283,
                -- Escorts Air SUP (SubCat 79) -> L1/L2/L3 Trips
                322, 323, 324,
                -- Escorts Ground SUP (SubCat 80) -> Trips, km
                331, 332, 333, 334, 335, 336, 337, 338,
                -- Holding SUP (SubCat 81) -> Cell Block Hours
                339
            );
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally irreversible. The deleted records referenced metrics that have been
        // moved to the Location Level form and can be re-entered there.
    }
}
