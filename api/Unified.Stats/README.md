# Unified.Stats

ASP.NET Core module for BC Sheriff Stats data entry. Enables sheriffs to record operational statistics by location and period.

Enabled via feature flag:

```json
{
  "FeatureFlags": {
    "StatsModule": true
  }
}
```

## Data Model

```
StatGroup
  └── StatCategory (GroupId)
        └── SubCategory (CategoryId)
              └── SubCategoryMetric (SubCategoryId, MetricId)
                    └── StatRecord (SubCategoryMetricId, LocationId)

StatMetric  ──────────────────────────┘ (MetricId)
StatSignoff (UserId, LocationId, Month, Year)
```

- **StatGroup** — top-level grouping (Non-Supervision, Supervision)
- **StatCategory** — category within a group; supports `IsArchived` and `IsHighSecurity` flags
- **SubCategory** — sub-division of a category; categories with no natural sub-division use a `General` placeholder
- **StatMetric** — a named unit of measurement (e.g. "Staff Hours", "Number of Trips")
- **SubCategoryMetric** — links a SubCategory to a Metric with a display order; this is the leaf node that `StatRecord` points to
- **StatRecord** — a data entry row: a value recorded for a SubCategoryMetric at a Location over a date range
- **StatSignoff** — records that a user has signed off all stats for a Location/Month/Year

## Reference Data vs. Data Entry

| Entity            | Type               | API                             |
| ----------------- | ------------------ | ------------------------------- |
| StatGroup         | Reference (seeded) | GET only                        |
| StatCategory      | Reference (seeded) | GET only                        |
| SubCategory       | Reference (seeded) | GET only                        |
| StatMetric        | Reference (seeded) | GET only                        |
| SubCategoryMetric | Reference (seeded) | GET only                        |
| StatRecord        | Data entry         | Full CRUD + test data generator |
| StatSignoff       | Data entry         | Full CRUD                       |

Reference data is managed exclusively through seeders. Adding, renaming, or retiring entries requires a seeder change and deployment — this is intentional so that structural changes go through code review.

## BCSS Oracle ETL model

The `etl` PostgreSQL schema contains the persistent configuration and staging
model for exporting Unified statistics to Oracle:

- `BiOracleLocationMapping` maps a Unified location's four-character JUSTIN
  location code to the legacy Oracle `CRT_LOC_ID`.
- `BiOracleStatMappingSets` stores effective-dated mapping sets indefinitely.
- `BiOracleStatMappings` explicitly maps `SubCategoryMetric` records to physical
  Oracle table/column targets and is retained indefinitely.
- `BiOracleEtlRuns` records one export execution and the mapping set it used.
- `BiOracleStatStages` stores one canonical aggregated destination cell per run.

Run counts have these meanings:

- `SourceRecordCount`: Unified `StatRecord` rows considered for the run.
- `StagedCellCount`: final canonical rows written to `BiOracleStatStages`.
- `LoadedCellCount`: staged destination cells successfully applied to Oracle.

`BiOracleEtlRuns` and their cascading stage rows are retained for three months.
No existing recurring cleanup mechanism naturally owns this data, so scheduling
the purge remains an operational follow-up. The cleanup should delete runs with
`StartedAt` older than the approved three-month cutoff; stage rows cascade. It
must never delete mapping sets or mappings.

The `BiOracleMappingsDataSet` provides one authoritative 661-row mapping set
with ID 1, covering all 296 active statistic columns in the
`BCSS_Table_Mapping` worksheet of
`BCSS Tables and Fields (Historic Mapping) CURRENT_20260916.xlsx`. It also contains
119 location mappings (118 legacy mappings plus
Ahousaht Provincial Court) from JUSTIN location codes to CORIN location IDs. The
initial stat mapping effective date is 2026-11-01, with no expiry, regardless of
when seeding first runs. Persisted mappings remain immutable. Deployments that
require the BI Oracle mappings select this data set through the central `SeedData:DataSets`
mechanism.

### Expanded legacy statistics

Holding area/cellblock (Location Level category 29) retains SCM 407 / Metric 14
under Hours. Its six demographic subcategories use only Metrics 33 and 34.
Transports Air (27) and Transports Ground (28) each retain their existing trip and
kilometre SCMs under Trips and Trips & Kilometres, respectively. Six demographic
subcategories in each expose the four existing transport classification metrics.
SCMs 449-508 represent these 60 independent demographic/classification cells;
subcategories 117-134 hold them. SCM 509 adds Circuit Court trips (Metric 29).
Supervision category 37 / subcategory 135 records non-frontline supervisory duties
using SCMs 510-511 (regular and overtime hours). Documents and Others use the
existing, distinct Received/Concluded SCMs 435-438 and 443-446.

Mapping IDs 1-516 retain their original source/target associations; IDs 517-661
add the missing targets and Auto Sum contributions. The workbook explicitly
excludes Federal transport counts from the Provincial adult/youth demographic
totals and the Level 1/2 totals.
Federal cells map only to their own detailed columns. Air and Ground staff-hour
totals sum the existing L1/L2/L3 regular and overtime SCMs. No duplicate total entry
fields are introduced. Archived legacy columns and audit/control fields, including
NIL_REPORT, are excluded. There is no checked-in ETL aggregation/loader implementation
in this repository; the effective-dated mapping rows define those source contributions.

The initial release includes the complete entry taxonomy and mapping configuration.
No historical coarse counts are split or inferred.
SCMs 408-413 and 418-427, and the Location Level male/female transport entry paths,
are archived, never deleted or reassigned. The normal entry form uses active
metadata only. Weekly reconstruction skips records absent from that metadata;
`SaveDayAsync` preserves omitted records whose SCM, subcategory, or category is
archived, including when signed-off override is available. Historical records remain
available through record/search APIs; active records on the same day remain editable.
Retained archived records follow the day's Draft/Submitted status transition using
the existing signed-off override rule, without changing their values or associations.

No schema or EF migration is required. New category/subcategory IDs stay below
their generated-ID boundary of 200. SCM IDs 449-511 cross their original sequence
start, so the SCM seeder advances the existing PostgreSQL identity sequence to at
least the table maximum without rewinding it. Mapping IDs 1-661 remain below
their original generated-ID start of 1000 and need no sequence advancement. An
existing SCM with a conflicting new seed ID causes seeding to fail rather than repurposing it;
mapping primary keys likewise cannot overwrite unrelated historical rows.

Both location-code values are unique in the seeded mappings. ETL location selection
uses `PerformedAtLocationId ?? LocationId` to find the `Locations` row in that
environment, then maps its `JustinLocationCode` through this table. The mapping
has no EF foreign key to `Locations`; its seed IDs are mapping-row IDs, not
Unified location IDs. The legacy mappings' effective and expiry dates come from
`SHF_LOCATIONS`; Ahousaht Provincial Court (AgencyId `19844.0877`) uses
1989-01-01 with no expiry.

The seed data preserves 1989-01-01 as the shared legacy baseline, while explicitly
dated locations retain their individual dates. The checked-in mapping data and
documentation do not substantiate why 1989 was originally chosen. New location
mapping definitions must supply an effective date explicitly. JUSTIN mapping codes
must be exactly four characters without surrounding whitespace; validation rejects
invalid codes rather than trimming or changing their case.

### One-time development/test mapping reset

Fresh databases seed the complete configuration without cleanup. Development/test
databases containing an earlier version of mapping set 1 or the PR's former
mapping set 2 require an explicit reset of that unused BI mapping configuration.
The normal seeder deliberately rejects changes to persisted mappings and does
not perform this reset.

Stop the API and any ETL process, connect to the intended development/test
PostgreSQL database, and inspect the existing sets and mappings:

```sql
SELECT s."Id", s."EffectiveDate", s."ExpiryDate",
       COUNT(m."Id") AS mapping_count, MIN(m."Id") AS first_mapping_id,
       MAX(m."Id") AS last_mapping_id
FROM etl."BiOracleStatMappingSets" s
LEFT JOIN etl."BiOracleStatMappings" m ON m."MappingSetId" = s."Id"
GROUP BY s."Id", s."EffectiveDate", s."ExpiryDate"
ORDER BY s."Id";

SELECT r."Id", r."LoadId", r."MappingSetId", r."ReportingMonth",
       r."Status", r."StartedAt", COUNT(c."Id") AS staged_cell_count
FROM etl."BiOracleEtlRuns" r
LEFT JOIN etl."BiOracleStatStages" c ON c."LoadId" = r."LoadId"
WHERE r."MappingSetId" IN (1, 2)
GROUP BY r."Id", r."LoadId", r."MappingSetId", r."ReportingMonth",
         r."Status", r."StartedAt"
ORDER BY r."Id";
```

If there are ETL run references, stop and resolve their retention with the data
owner before resetting; the cleanup below refuses to delete referenced sets.
If the configuration is already the current set 1 with 661 matching mappings,
effective 2026-11-01 with no expiry, no reset is needed. Otherwise, after confirming
sets 1 and 2 contain only the unused development/test configuration, run:

```sql
BEGIN;
LOCK TABLE etl."BiOracleStatMappingSets", etl."BiOracleStatMappings",
           etl."BiOracleEtlRuns" IN SHARE ROW EXCLUSIVE MODE;
DO $reset$
BEGIN
    IF EXISTS (SELECT 1 FROM etl."BiOracleEtlRuns" WHERE "MappingSetId" IN (1, 2)) THEN
        RAISE EXCEPTION 'BI mapping reset refused: mapping sets are referenced by ETL runs';
    END IF;
END;
$reset$;
DELETE FROM etl."BiOracleStatMappings" WHERE "MappingSetId" IN (1, 2);
DELETE FROM etl."BiOracleStatMappingSets" WHERE "Id" IN (1, 2);
COMMIT;
```

Restart the API with `FeatureFlags:Stats:Enabled` enabled and
`BiOracleMappingsDataSet` included in the existing `SeedData:DataSets` selection.
Normal startup seeding recreates set 1 and mappings 1-661. Re-run the inspection
query to confirm the date, expiry, count, and ID range. No identity-sequence reset
is required. This procedure leaves ETL runs/stages, location mappings, StatRecords,
and all other Stats reference data untouched.

## Seeders

Seeders live in `Unified.Stats/Seeders/`, extend `SeederBase<UnifiedDbContext>`, and are explicitly registered by `StatsModule`.

### Execution Order

| Order | Seeder                          | Records             | Description                                 |
| ----- | ------------------------------- | ------------------- | ------------------------------------------- |
| 10    | `StatGroupSeeder`               | 2                   | Top-level groups                            |
| 11    | `StatCategorySeeder`            | 25                  | Categories per group                        |
| 12    | `SubCategorySeeder`             | 91                  | Sub-categories per category                 |
| 13    | `StatMetricSeeder`              | 49                  | Deduplicated metrics                        |
| 14    | `SubCategoryMetricSeeder`       | Configured taxonomy | Sub-category/metric leaf mappings           |
| 15    | `BiOracleLocationMappingSeeder` | 119                 | JUSTIN-to-Oracle location mappings          |
| 16    | `BiOracleStatMappingSeeder`     | 661                 | One authoritative Oracle mapping set        |

Stats seeders run after the core UserManagement seeders (User=0, Region=1, Location=2).

### Seeded Data Summary

**Groups**

| Id  | Name            |
| --- | --------------- |
| 1   | Non-Supervision |
| 2   | Supervision     |

**Categories (25 total)**

| Ids   | Group           | Categories                                                                                                                                                                                                                                                                           |
| ----- | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1–14  | Non-Supervision | Court Security, Circuit court related travel, Coroner Jury Administration, Criminal/Civil Jury Administration, Documents Civil/Family, Documents Criminal, Transports Air, Transports Ground, Transports Females, Transports Males, Holding area/cellblock, Other, PIO/SIO, Training |
| 15–25 | Supervision     | Court Security, Circuit court related travel, Documents Civil/Family, Documents Criminal, Transports Air, Transports Ground, Holding area/cellblock, Jury Administration, Other, PIO/SIO, Training                                                                                   |

**Sub-Categories (91 total)**

Categories with no natural sub-division use a `General` placeholder (ids 18, 19, 20, 50, 89) so that `StatRecord` always points to a `SubCategoryMetric` rather than directly to a category.

| Id range | Category                                                 |
| -------- | -------------------------------------------------------- |
| 1–17     | Court Security (Non-Supervision) — 17 court types        |
| 18       | Circuit court related travel (Non-Supervision) — General |
| 19       | Coroner Jury Administration — General                    |
| 20       | Criminal/Civil Jury Administration — General             |
| 21–24    | Documents Civil/Family (Non-Supervision)                 |
| 25–28    | Documents Criminal (Non-Supervision)                     |
| 29       | Transports Air (Non-Supervision)                         |
| 30       | Transports Ground (Non-Supervision)                      |
| 31–33    | Transports Females                                       |
| 34–36    | Transports Males                                         |
| 37–43    | Holding area/cellblock (Non-Supervision)                 |
| 44–49    | Other (Non-Supervision)                                  |
| 50       | PIO/SIO (Non-Supervision) — General                      |
| 51–52    | Training (Non-Supervision)                               |
| 53–69    | Court Security (Supervision) — 17 court types            |
| 70       | Circuit court related travel (Supervision) — Hours       |
| 71–74    | Documents Civil/Family (Supervision)                     |
| 75–78    | Documents Criminal (Supervision)                         |
| 79       | Transports Air (Supervision)                             |
| 80       | Transports Ground (Supervision)                          |
| 81       | Holding area/cellblock (Supervision) — Hours             |
| 82       | Jury Administration (Supervision) — Hours                |
| 83–88    | Other (Supervision)                                      |
| 89       | PIO/SIO (Supervision) — General                          |
| 90–91    | Training (Supervision)                                   |

**Metrics (49 total)**

| Id range | Unit                         | Examples                                                                     |
| -------- | ---------------------------- | ---------------------------------------------------------------------------- |
| 1–23     | `hours`                      | Staff Hours, Overtime Hours, Level 1/2/3 Staff Hours, Instructor Hours, etc. |
| 24–42    | `count`                      | Number of Trips, Jurors Summonsed, Custodies, Level 1/2/3 Air/Ground, etc.   |
| 43–46    | `km`                         | Number of km Travelled, Level 1/2/3 Ground km                                |
| 47       | `$`                          | Sum Total ($) Paid to Jurors and Alternates                                  |
| 48–49    | `count (received/concluded)` | Received, Concluded                                                          |

### Identity Sequence Conflict Prevention

Seeded rows use explicit IDs starting at 1. To prevent the PostgreSQL identity sequence from colliding with seeded IDs on subsequent inserts, these tables reserve generated IDs above their seeded ranges:

- `StatGroups`, `StatCategories`, `SubCategories`, `StatMetrics`, `etl.BiOracleLocationMapping`, and `etl.BiOracleStatMappingSets` start at 200.
- `SubCategoryMetrics` originally starts at 500 (seeded IDs through 511).
- `etl.BiOracleStatMappings` starts at 1000 (seeded IDs 1-661).

This is configured in the EF entity configurations via `HasIdentityOptions(startValue: ...)` and reflected in the migration's `Npgsql:IdentitySequenceOptions` annotations.
The expanded SCM range is handled by advancing its existing sequence during
seeding as described above; the model and migration annotations remain unchanged.

### Adding or Changing Reference Data

1. Update the relevant seeder's static array (add, remove, or edit an entry)
2. If the change involves new IDs beyond the current max, verify the table's identity sequence start still leaves enough headroom
3. Deploy — the seeder will upsert on next startup
