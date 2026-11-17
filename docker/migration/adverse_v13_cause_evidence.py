"""Read-only fixture causes, NOT a source-business comparer or an SH extractor."""
import json
from adverse_valid_v11_evidence import ARTIFACTS, read

OUTPUT = ARTIFACTS / "adverse-valid-v13-causes.json"
RUN = "c32d306f-a6f2-40eb-8fcd-49d1c01376ea"


def main():
    if OUTPUT.exists():
        raise RuntimeError("Cause evidence is immutable")
    rejected = read("target", f"""SELECT "LegacyId","Reason" FROM "DataMigrationOutcomes"
        WHERE "RunId"='{RUN}' AND "EntityType"='AssignmentSeries' AND "Outcome"='Rejected'""")
    ids = [int(x["LegacyId"].removeprefix("g10:assignment:")) for x in rejected]
    if len(ids) != 33 or any(x["Reason"] != "InconsistentSeriesMember" for x in rejected):
        raise RuntimeError("Retained series rejection inventory changed")
    # These are the migration's source eligibility/representation preconditions,
    # not reconstructed SS availability, overtime or business outcomes.
    series = read("source", f"""
        WITH candidates AS (
          SELECT a."Id" AS assignment,d."Id" AS duty,d."LocationId" AS location,
            (d."StartDate" AT TIME ZONE a."Timezone")::date AS day,
            row_number() OVER (PARTITION BY a."Id",(d."StartDate" AT TIME ZONE a."Timezone")::date
              ORDER BY d."StartDate",d."Id") AS n
          FROM "Assignment" a JOIN "Duty" d ON d."AssignmentId"=a."Id"
          WHERE a."Id" IN ({','.join(map(str,ids))}) AND d."ExpiryDate" IS NULL
          AND (d."StartDate" AT TIME ZONE a."Timezone")::date > DATE '2026-12-01'
          AND (d."StartDate" AT TIME ZONE a."Timezone")::time=a."Start"
          AND (d."EndDate" AT TIME ZONE a."Timezone")::time=a."End"
          AND (d."StartDate" AT TIME ZONE a."Timezone")::date=(d."EndDate" AT TIME ZONE a."Timezone")::date
          AND CASE extract(dow FROM d."StartDate" AT TIME ZONE a."Timezone")::int
            WHEN 0 THEN a."Sunday" WHEN 1 THEN a."Monday" WHEN 2 THEN a."Tuesday"
            WHEN 3 THEN a."Wednesday" WHEN 4 THEN a."Thursday" WHEN 5 THEN a."Friday" ELSE a."Saturday" END
          AND (a."AdhocStartDate" IS NULL OR (d."StartDate" AT TIME ZONE a."Timezone")::date >=
              (a."AdhocStartDate" AT TIME ZONE a."Timezone")::date)
          AND (a."AdhocEndDate" IS NULL OR (d."StartDate" AT TIME ZONE a."Timezone")::date <=
              (a."AdhocEndDate" AT TIME ZONE a."Timezone")::date)
        ), represented AS (
          SELECT c.*,count(s."Id") AS live_slots,
            CASE WHEN count(s."Id")=1 AND bool_or(s."IsClosed" OR s."IsNotRequired") THEN 0 ELSE 1 END AS capacity
          FROM candidates c LEFT JOIN "DutySlot" s ON s."DutyId"=c.duty
            AND s."ExpiryDate" IS NULL AND s."EndDate">=TIMESTAMPTZ '2015-01-01T08:00:00Z'
          WHERE c.n=1 GROUP BY c.assignment,c.duty,c.location,c.day,c.n
        )
        SELECT assignment,count(*) AS members,count(DISTINCT capacity) AS capacities,
          count(DISTINCT location) AS locations,max(live_slots) AS max_live_slots
        FROM represented GROUP BY assignment ORDER BY assignment""")
    failed = read("target", f"""SELECT "LegacyId","Reason" FROM "DataMigrationOutcomes"
        WHERE "RunId"='{RUN}' AND "EntityType"='DutySlot' AND "Outcome"='Failed'""")
    if len(failed) != 1:
        raise RuntimeError("Retained physical failure inventory changed")
    slot = int(failed[0]["LegacyId"])
    details = read("source", f"""
        SELECT s."Id" AS slot,s."StartDate" AS start,s."EndDate" AS ending,
          s."Timezone" AS timezone,
          EXISTS(SELECT 1 FROM "User" u WHERE u."Id"=s."SheriffId") AS source_user_exists,
          (SELECT count(*) FROM synthetic.case_manifest m
             WHERE m.source_table='DutySlot' AND m.source_id=s."Id"::text) AS manifest_rows,
          coalesce(json_agg(sh."Id") FILTER(WHERE sh."Id" IS NOT NULL),'[]'::json) AS overlapping_shift_ids
        FROM "DutySlot" s JOIN "Duty" d ON d."Id"=s."DutyId"
        LEFT JOIN "Shift" sh ON sh."SheriffId"=s."SheriffId" AND sh."LocationId"=d."LocationId"
          AND sh."StartDate"<s."EndDate" AND sh."EndDate">s."StartDate"
          AND sh."EndDate">=TIMESTAMPTZ '2015-01-01T08:00:00Z'
        WHERE s."Id"={slot} GROUP BY s."Id" """)
    shift_ids = [str(x) for x in details[0]["overlapping_shift_ids"]]
    shift_outcomes = read("target", f"""SELECT "Outcome","Reason",count(*) AS rows
        FROM "DataMigrationOutcomes" WHERE "RunId"='{RUN}' AND "EntityType"='Shift'
        AND "LegacyId" IN ({','.join("'" + str(int(x)) + "'" for x in shift_ids) or "NULL"})
        GROUP BY 1,2""")
    summary = dict(
        actual_rejected_series=len(ids), source_candidate_groups=len(series),
        groups_with_mixed_capacity=sum(x["capacities"] > 1 for x in series),
        groups_with_mixed_location=sum(x["locations"] > 1 for x in series),
        groups_with_multiple_live_slots=sum(x["max_live_slots"] > 1 for x in series),
        failed_slot_reason=failed[0]["Reason"],
        failed_slot_start=details[0]["start"], failed_slot_end=details[0]["ending"],
        failed_slot_timezone=details[0]["timezone"],
        failed_slot_source_user_exists=details[0]["source_user_exists"],
        failed_slot_manifest_rows=details[0]["manifest_rows"],
        failed_slot_overlapping_source_shifts=len(shift_ids), overlapping_shift_outcomes=shift_outcomes,
        evidence_scope="Actual retained rejection + read-only source representation preconditions; not business parity",
        acceptance=False)
    OUTPUT.write_text(json.dumps(dict(summary=summary, series=series, failed_slot=details), indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
