using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Db.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCalendarEventExceptionReasonCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail before dropping legacy data if a deployment contains an unmapped reason.
            // The only legacy reason written by the application is IsAway.
            migrationBuilder.Sql(
                """
                LOCK TABLE "Events" IN ACCESS EXCLUSIVE MODE;
                DO $$
                DECLARE
                    unknown_reasons text;
                    inconsistent_away_ids text;
                    unexplained_exception_ids text;
                BEGIN
                    SELECT string_agg(quote_literal(reason), ', ' ORDER BY reason)
                    INTO unknown_reasons
                    FROM (SELECT DISTINCT "ExceptionReason" AS reason FROM "Events"
                          WHERE "ExceptionReason" IS NOT NULL AND "ExceptionReason" <> 'IsAway') legacy;
                    IF unknown_reasons IS NOT NULL THEN
                        RAISE EXCEPTION 'Unmapped Events.ExceptionReason values: %. Migration aborted; legacy data retained.', unknown_reasons;
                    END IF;

                    SELECT string_agg("Id"::text, ', ' ORDER BY "Id")
                    INTO inconsistent_away_ids
                    FROM "Events"
                    WHERE NOT "IsException" AND "ExceptionReason" = 'IsAway';

                    -- False flags on historical time deviations are intentionally reconstructable.
                    SELECT string_agg("Id"::text, ', ' ORDER BY "Id")
                    INTO unexplained_exception_ids
                    FROM "Events"
                    WHERE "IsException" AND "ExceptionReason" IS NULL
                      AND NOT ("EventSeriesId" IS NOT NULL
                               AND ("StartAtUtc" IS DISTINCT FROM "SeriesStartAtUtc"
                                    OR "EndAtUtc" IS DISTINCT FROM "SeriesEndAtUtc"));

                    IF inconsistent_away_ids IS NOT NULL OR unexplained_exception_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Ambiguous legacy event exceptions. IsException=false with IsAway (event IDs: %); IsException=true without a recognized reason or applicable date/time deviation (event IDs: %). Migration aborted; legacy data retained.',
                            coalesce(inconsistent_away_ids, 'none'), coalesce(unexplained_exception_ids, 'none');
                    END IF;
                END $$;
                """
            );

            migrationBuilder.AddColumn<string[]>(
                name: "ExceptionReasonCodes",
                table: "Events",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]"
            );

            // Reconstruct time deviations independently of the old flag, which loan-out could overwrite.
            migrationBuilder.Sql(
                """
                UPDATE "Events"
                SET "ExceptionReasonCodes" = array_remove(ARRAY[
                    CASE WHEN "EventSeriesId" IS NOT NULL
                              AND ("StartAtUtc" IS DISTINCT FROM "SeriesStartAtUtc"
                                   OR "EndAtUtc" IS DISTINCT FROM "SeriesEndAtUtc")
                         THEN 'DateTimeChanged' END,
                    CASE WHEN "ExceptionReason" = 'IsAway' THEN 'IsAway' END
                ], NULL);
                """
            );

            migrationBuilder.DropColumn(name: "ExceptionReason", table: "Events");
            migrationBuilder.DropColumn(name: "IsException", table: "Events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExceptionReason",
                table: "Events",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsException",
                table: "Events",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            // Keep the legacy away marker and flag. Time deviations remain recoverable from the baselines.
            migrationBuilder.Sql(
                """
                UPDATE "Events"
                SET "IsException" = cardinality("ExceptionReasonCodes") > 0,
                    "ExceptionReason" = CASE WHEN 'IsAway' = ANY("ExceptionReasonCodes") THEN 'IsAway' END;
                """
            );
            migrationBuilder.DropColumn(name: "ExceptionReasonCodes", table: "Events");
        }
    }
}
