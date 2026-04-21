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

| Entity | Type | API |
|---|---|---|
| StatGroup | Reference (seeded) | GET only |
| StatCategory | Reference (seeded) | GET only |
| SubCategory | Reference (seeded) | GET only |
| StatMetric | Reference (seeded) | GET only |
| SubCategoryMetric | Reference (seeded) | GET only |
| StatRecord | Data entry | Full CRUD + test data generator |
| StatSignoff | Data entry | Full CRUD |

Reference data is managed exclusively through seeders. Adding, renaming, or retiring entries requires a seeder change and deployment — this is intentional so that structural changes go through code review.

## Seeders

Seeders live in `Unified.Stats/Seeders/` and extend `SeederBase<UnifiedDbContext>`. They are auto-discovered at startup by `SeederFactory` via reflection — no manual registration is required.

### Execution Order

| Order | Seeder | Records | Description |
|-------|--------|---------|-------------|
| 10 | `StatGroupSeeder` | 2 | Top-level groups |
| 11 | `StatCategorySeeder` | 25 | Categories per group |
| 12 | `SubCategorySeeder` | 91 | Sub-categories per category |
| 13 | `StatMetricSeeder` | 49 | Deduplicated metrics |

Stats seeders run after the core UserManagement seeders (User=0, Region=1, Location=2).

### Seeded Data Summary

**Groups**

| Id | Name |
|----|------|
| 1 | Non-Supervision |
| 2 | Supervision |

**Categories (25 total)**

| Ids | Group | Categories |
|-----|-------|-----------|
| 1–14 | Non-Supervision | Court Security, Circuit court related travel, Coroner Jury Administration, Criminal/Civil Jury Administration, Documents Civil/Family, Documents Criminal, Escorts Air, Escorts Ground, Escorts females, Escorts males, Holding area/cellblock, Other, PIO/SIO, Training |
| 15–25 | Supervision | Court Security, Circuit court related travel, Documents Civil/Family, Documents Criminal, Escorts Air, Escorts Ground, Holding area/cellblock, Jury Administration, Other, PIO/SIO, Training |

**Sub-Categories (91 total)**

Categories with no natural sub-division use a `General` placeholder (ids 18, 19, 20, 50, 89) so that `StatRecord` always points to a `SubCategoryMetric` rather than directly to a category.

| Id range | Category |
|----------|---------|
| 1–17 | Court Security (Non-Supervision) — 17 court types |
| 18 | Circuit court related travel (Non-Supervision) — General |
| 19 | Coroner Jury Administration — General |
| 20 | Criminal/Civil Jury Administration — General |
| 21–24 | Documents Civil/Family (Non-Supervision) |
| 25–28 | Documents Criminal (Non-Supervision) |
| 29 | Escorts Air (Non-Supervision) |
| 30 | Escorts Ground (Non-Supervision) |
| 31–33 | Escorts females |
| 34–36 | Escorts males |
| 37–43 | Holding area/cellblock (Non-Supervision) |
| 44–49 | Other (Non-Supervision) |
| 50 | PIO/SIO (Non-Supervision) — General |
| 51–52 | Training (Non-Supervision) |
| 53–69 | Court Security (Supervision) — 17 court types |
| 70 | Circuit court related travel (Supervision) — Hours |
| 71–74 | Documents Civil/Family (Supervision) |
| 75–78 | Documents Criminal (Supervision) |
| 79 | Escorts Air (Supervision) |
| 80 | Escorts Ground (Supervision) |
| 81 | Holding area/cellblock (Supervision) — Hours |
| 82 | Jury Administration (Supervision) — Hours |
| 83–88 | Other (Supervision) |
| 89 | PIO/SIO (Supervision) — General |
| 90–91 | Training (Supervision) |

**Metrics (49 total)**

| Id range | Unit | Examples |
|----------|------|---------|
| 1–23 | `hours` | Staff Hours, Overtime Hours, Level 1/2/3 Staff Hours, Instructor Hours, etc. |
| 24–42 | `count` | Number of Trips, Jurors Summonsed, Custodies, Level 1/2/3 Air/Ground, etc. |
| 43–46 | `km` | Number of km Travelled, Level 1/2/3 Ground km |
| 47 | `$` | Sum Total ($) Paid to Jurors and Alternates |
| 48–49 | `count (received/concluded)` | Received, Concluded |

### Identity Sequence Conflict Prevention

Seeded rows use explicit IDs starting at 1. To prevent the PostgreSQL identity sequence from colliding with seeded IDs on subsequent inserts, the following tables have their identity sequence set to start at 200:

- `StatGroups`, `StatCategories`, `SubCategories`, `StatMetrics`

This is configured in the EF entity configurations via `HasIdentityOptions(startValue: 200)` and reflected in the migration annotation `"Npgsql:IdentitySequenceOptions", "'200', '1', '', '', 'False', '1'"`.

### Adding or Changing Reference Data

1. Update the relevant seeder's static array (add, remove, or edit an entry)
2. If the change involves new IDs beyond the current max, verify the identity `startValue: 200` still leaves enough headroom
3. Deploy — the seeder will upsert on next startup