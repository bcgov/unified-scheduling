using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Unified.Db;

#nullable disable

namespace Unified.Db.Migrations;

[DbContext(typeof(UnifiedDbContext))]
[Migration("20260730000000_RemoveSeededStatutoryHolidayEvents")]
public partial class RemoveSeededStatutoryHolidayEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM "Events" AS event
            USING (
                VALUES
                    ('New Year''s Day', TIMESTAMPTZ '2026-01-01 00:00:00+00', TIMESTAMPTZ '2026-01-02 00:00:00+00'),
                    ('Family Day', TIMESTAMPTZ '2026-02-16 00:00:00+00', TIMESTAMPTZ '2026-02-17 00:00:00+00'),
                    ('Good Friday', TIMESTAMPTZ '2026-04-03 00:00:00+00', TIMESTAMPTZ '2026-04-04 00:00:00+00'),
                    ('Victoria Day', TIMESTAMPTZ '2026-05-18 00:00:00+00', TIMESTAMPTZ '2026-05-19 00:00:00+00'),
                    ('Canada Day', TIMESTAMPTZ '2026-07-01 00:00:00+00', TIMESTAMPTZ '2026-07-02 00:00:00+00'),
                    ('B.C. Day', TIMESTAMPTZ '2026-08-03 00:00:00+00', TIMESTAMPTZ '2026-08-04 00:00:00+00'),
                    ('Labour Day', TIMESTAMPTZ '2026-09-07 00:00:00+00', TIMESTAMPTZ '2026-09-08 00:00:00+00'),
                    ('National Day for Truth and Reconciliation', TIMESTAMPTZ '2026-09-30 00:00:00+00', TIMESTAMPTZ '2026-10-01 00:00:00+00'),
                    ('Thanksgiving Day', TIMESTAMPTZ '2026-10-12 00:00:00+00', TIMESTAMPTZ '2026-10-13 00:00:00+00'),
                    ('Remembrance Day', TIMESTAMPTZ '2026-11-11 00:00:00+00', TIMESTAMPTZ '2026-11-12 00:00:00+00'),
                    ('Christmas Day', TIMESTAMPTZ '2026-12-25 00:00:00+00', TIMESTAMPTZ '2026-12-26 00:00:00+00'),
                    ('New Year''s Day', TIMESTAMPTZ '2027-01-01 00:00:00+00', TIMESTAMPTZ '2027-01-02 00:00:00+00'),
                    ('Family Day', TIMESTAMPTZ '2027-02-15 00:00:00+00', TIMESTAMPTZ '2027-02-16 00:00:00+00'),
                    ('Good Friday', TIMESTAMPTZ '2027-03-26 00:00:00+00', TIMESTAMPTZ '2027-03-27 00:00:00+00'),
                    ('Victoria Day', TIMESTAMPTZ '2027-05-24 00:00:00+00', TIMESTAMPTZ '2027-05-25 00:00:00+00'),
                    ('Canada Day', TIMESTAMPTZ '2027-07-01 00:00:00+00', TIMESTAMPTZ '2027-07-02 00:00:00+00'),
                    ('B.C. Day', TIMESTAMPTZ '2027-08-02 00:00:00+00', TIMESTAMPTZ '2027-08-03 00:00:00+00'),
                    ('Labour Day', TIMESTAMPTZ '2027-09-06 00:00:00+00', TIMESTAMPTZ '2027-09-07 00:00:00+00'),
                    ('National Day for Truth and Reconciliation', TIMESTAMPTZ '2027-09-30 00:00:00+00', TIMESTAMPTZ '2027-10-01 00:00:00+00'),
                    ('Thanksgiving Day', TIMESTAMPTZ '2027-10-11 00:00:00+00', TIMESTAMPTZ '2027-10-12 00:00:00+00'),
                    ('Remembrance Day', TIMESTAMPTZ '2027-11-11 00:00:00+00', TIMESTAMPTZ '2027-11-12 00:00:00+00'),
                    ('Christmas Day', TIMESTAMPTZ '2027-12-25 00:00:00+00', TIMESTAMPTZ '2027-12-26 00:00:00+00')
            ) AS legacy("Title", "StartAtUtc", "EndAtUtc")
            WHERE event."Title" = legacy."Title"
              AND event."StartAtUtc" = legacy."StartAtUtc"
              AND event."EndAtUtc" = legacy."EndAtUtc"
              AND event."SourceModule" = 'calendar'
              AND event."EventTypeCode" = 'holiday'
              AND event."StatusTypeCode" = 'active'
              AND event."AllDay" = TRUE
              AND event."IsException" = FALSE
              AND event."EventSeriesId" IS NULL
              AND event."Description" IS NULL
              AND event."Notes" IS NULL
              AND event."Color" IS NULL
              AND event."TimeZoneId" IS NULL
              AND event."CancelledAt" IS NULL
              AND event."CancelledByUserId" IS NULL
              AND event."CancellationReason" IS NULL
              AND event."LocationId" IS NULL
              AND event."CreatedById" IS NULL
              AND event."UpdatedById" IS NULL
              AND event."UpdatedOn" IS NULL;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally irreversible. The deleted records were generated from obsolete seed data and cannot be
        // restored without reintroducing that retired source.
    }
}
