\set ON_ERROR_STOP on
SELECT set_config('ss.probe.scenario', :'scenario', false);
DO $$
DECLARE scenario text := current_setting('ss.probe.scenario');
BEGIN
 IF current_database() NOT IN ('unified_ss_rehearsal_repartition_v4','unified_ss_rehearsal_repartition_v5') THEN RAISE EXCEPTION 'Isolated target required'; END IF;
 IF EXISTS (
  SELECT 1 FROM "DataMigrationRecords" r WHERE r."Source"='SS' AND (
   r."TargetType"='AssignmentEntry' AND NOT EXISTS(SELECT 1 FROM "AssignmentEntries" t WHERE t."Id"::text=r."TargetKey") OR
   r."TargetType"='AssignmentDefinition' AND NOT EXISTS(SELECT 1 FROM "AssignmentDefinitions" t WHERE t."Id"::text=r."TargetKey") OR
   r."TargetType"='AssignmentSeries' AND NOT EXISTS(SELECT 1 FROM "AssignmentSeries" t WHERE t."Id"::text=r."TargetKey") OR
   r."TargetType"='ShiftEntry' AND NOT EXISTS(SELECT 1 FROM "ShiftEntries" t WHERE t."Id"::text=r."TargetKey")
  )) THEN RAISE EXCEPTION 'Dangling crosswalk'; END IF;
 IF EXISTS (SELECT 1 FROM "AssignmentEntries" e WHERE NOT EXISTS(
  SELECT 1 FROM "DataMigrationRecords" r WHERE r."Source"='SS' AND r."EntityType"='DutyEntry' AND r."TargetKey"=e."Id"::text AND r."CreatedByRunId" IS NOT NULL))
 THEN RAISE EXCEPTION 'Unowned split entry'; END IF;
 IF EXISTS(SELECT 1 FROM "ShiftEntries" s WHERE NOT EXISTS(
  SELECT 1 FROM "DataMigrationRecords" r WHERE r."Source"='SS' AND r."EntityType"='Shift'
   AND r."TargetKey"=s."Id"::text AND r."CreatedByRunId" IS NOT NULL))
 THEN RAISE EXCEPTION 'Unowned/orphan shift root'; END IF;
 IF EXISTS(SELECT 1 FROM "AssignmentDefinitions" d WHERE NOT EXISTS(
  SELECT 1 FROM "DataMigrationRecords" r WHERE r."Source"='SS' AND r."EntityType"='Assignment'
   AND r."TargetKey"=d."Id"::text AND r."CreatedByRunId" IS NOT NULL))
 THEN RAISE EXCEPTION 'Unowned/orphan definition root'; END IF;
 IF EXISTS(SELECT 1 FROM "AssignmentSeries" s WHERE NOT EXISTS(
  SELECT 1 FROM "DataMigrationRecords" r WHERE r."Source"='SS' AND r."EntityType"='AssignmentSeries'
   AND r."TargetKey"=s."Id"::text AND r."CreatedByRunId" IS NOT NULL))
 THEN RAISE EXCEPTION 'Unowned/orphan series root'; END IF;
 IF EXISTS (SELECT 1 FROM "Events" e WHERE e."SourceModule"='scheduling' AND (
  e."EventTypeCode"='assignment' AND NOT EXISTS(SELECT 1 FROM "AssignmentEntries" a WHERE a."EventId"=e."Id") OR
  e."EventTypeCode"='shift' AND NOT EXISTS(SELECT 1 FROM "ShiftEntries" a WHERE a."EventId"=e."Id")))
 THEN RAISE EXCEPTION 'Orphan scheduling event'; END IF;
 IF EXISTS (SELECT 1 FROM "EventSeries" e WHERE NOT EXISTS(SELECT 1 FROM "AssignmentSeries" a WHERE a."EventSeriesId"=e."Id"))
 THEN RAISE EXCEPTION 'Orphan event series'; END IF;
 IF EXISTS(SELECT 1 FROM "AssignmentEntries" a JOIN "Events" e ON e."Id"=a."EventId"
  WHERE a."AssignmentSeriesId" IS NOT NULL AND (e."StatusTypeCode"<>'draft' OR e."EventSeriesId" IS NULL))
 THEN RAISE EXCEPTION 'Invalid live recurrence membership'; END IF;
 IF scenario IN ('shift-split','shift-merge') AND
  ((SELECT count(DISTINCT "TargetKey") FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='Shift' AND "LegacyId" IN ('900000','900001')) <>
    CASE WHEN scenario='shift-split' THEN 2 ELSE 1 END)
 THEN RAISE EXCEPTION 'Shift membership not replayed'; END IF;
 IF scenario IN ('definition-merge','definition-split') AND
  ((SELECT count(DISTINCT "TargetKey") FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='Assignment' AND "LegacyId" IN ('900000','900010')) <>
    CASE WHEN scenario='definition-merge' THEN 1 ELSE 2 END)
 THEN RAISE EXCEPTION 'Definition membership not replayed'; END IF;
 IF scenario IN ('duty-multi','duty-multi-update','duty-single') AND
  ((SELECT count(*) FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='DutyEntry' AND "LegacyId" LIKE 'duty:900000:segment:%') <>
    CASE WHEN scenario IN ('duty-multi','duty-multi-update') THEN 2 ELSE 1 END)
 THEN RAISE EXCEPTION 'Duty graph cardinality not replayed'; END IF;
 IF scenario='duty-multi-update' AND NOT EXISTS(
  SELECT 1 FROM "DataMigrationRecords" r JOIN "AssignmentEntries" a ON a."Id"::text=r."TargetKey"
  JOIN "Events" e ON e."Id"=a."EventId"
  WHERE r."EntityType"='DutySlot' AND r."LegacyId"='900050' AND e."EndAtUtc"='2026-11-02T14:00:00Z')
 THEN RAISE EXCEPTION 'Changed multi-entry interval was not copied'; END IF;
 IF scenario='series-fallback' AND EXISTS(SELECT 1 FROM "DataMigrationRecords" WHERE "EntityType"='AssignmentSeries' AND "LegacyId"='g10:assignment:900010')
 THEN RAISE EXCEPTION 'Unqualified series retained'; END IF;
 IF scenario='series-qualified' AND NOT EXISTS(
  SELECT 1 FROM "DataMigrationRecords" r JOIN "AssignmentEntries" a ON a."AssignmentSeriesId"::text=r."TargetKey"
  WHERE r."EntityType"='AssignmentSeries' AND r."LegacyId"='g10:assignment:900010'
  GROUP BY r."Id" HAVING count(*)=2)
 THEN RAISE EXCEPTION 'Qualified series missing real occurrences'; END IF;
 IF scenario='series-three' AND NOT EXISTS(
  SELECT 1 FROM "DataMigrationRecords" r JOIN "AssignmentEntries" a ON a."AssignmentSeriesId"::text=r."TargetKey"
  WHERE r."EntityType"='AssignmentSeries' AND r."LegacyId"='g10:assignment:900010'
  GROUP BY r."Id" HAVING count(*)=3)
 THEN RAISE EXCEPTION 'Added real occurrence missing'; END IF;
 IF scenario='series-three' AND (
  SELECT array_agg(d."LegacyId"::text ORDER BY d."LegacyId")
  FROM "DataMigrationRecords" s JOIN "AssignmentEntries" a ON a."AssignmentSeriesId"::text=s."TargetKey"
  JOIN "DataMigrationRecords" d ON d."EntityType"='Duty' AND d."TargetKey"=a."Id"::text
  WHERE s."EntityType"='AssignmentSeries' AND s."LegacyId"='g10:assignment:900010'
 ) IS DISTINCT FROM ARRAY['900010','900011','900012']::text[]
 THEN RAISE EXCEPTION 'Series contains invented or missing source duty'; END IF;
 IF scenario='series-two-selected' AND (
  SELECT array_agg(d."LegacyId"::text ORDER BY d."LegacyId")
  FROM "DataMigrationRecords" s JOIN "AssignmentEntries" a ON a."AssignmentSeriesId"::text=s."TargetKey"
  JOIN "DataMigrationRecords" d ON d."EntityType"='Duty' AND d."TargetKey"=a."Id"::text
  WHERE s."EntityType"='AssignmentSeries' AND s."LegacyId"='g10:assignment:900010'
 ) IS DISTINCT FROM ARRAY['900010','900012']::text[]
 THEN RAISE EXCEPTION 'Changed weekdays did not select exactly the real qualifying duties'; END IF;
 RAISE NOTICE 'PASS %: crosswalks, owned entries, event/series footprint and membership', scenario;
END $$;
