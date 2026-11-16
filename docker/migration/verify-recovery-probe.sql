\set ON_ERROR_STOP on
-- Read-only verification of the preserved, named operational rehearsal.
-- These checks are PostgreSQL execution evidence, NOT additional xUnit tests.
BEGIN TRANSACTION READ ONLY;
DO $$ BEGIN
    IF current_database() <> 'unified_ss_rehearsal_ready_v4_probe' THEN
        RAISE EXCEPTION 'Named recovery probe target required.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "DataMigrationRuns"
        WHERE "Id"='20bb612b-4f2a-499a-94ee-3f1266aae8bc'
        AND "Status"='Failed' AND "CommittedBatchCount"=14 AND "ScannedCount"=28) THEN
        RAISE EXCEPTION 'Interrupted-run committed batch proof missing.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "DataMigrationWorkItems"
        WHERE "Source"='SS' AND "Job"='MyTeam' AND "Stream"='normal'
        AND "EntityType"='SheriffAwayLocation' AND "LegacyId"='900051'
        AND NOT "PendingRetry" AND "Attempts">=2) THEN
        RAISE EXCEPTION 'Independent below-cursor dependency retry did not resolve.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "DataMigrationWorkItems"
        WHERE "Source"='SS' AND "Stream"='backfill:2014-01-01'
        AND "EntityType"='SheriffLeave' AND "LegacyId"='900050' AND NOT "PendingRetry")
        OR NOT EXISTS (SELECT 1 FROM "DataMigrationRecords"
        WHERE "Source"='SS' AND "EntityType"='SheriffLeave' AND "LegacyId"='900050') THEN
        RAISE EXCEPTION 'Independent earlier-history backfill proof missing.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "DataMigrationOutcomes"
        WHERE "RunId"='79729120-3af5-49eb-a043-6d8a6b10813f'
        AND "Reason"='UnapprovedFutureAvailabilityLossOutsideSsInterval' AND "Outcome"='NeedsAction') THEN
        RAISE EXCEPTION 'Unapproved future availability impact was not held.';
    END IF;
    IF (SELECT count(*) FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='Shift'
        AND "LegacyId" IN ('900000','900001','900099')) <> 3
        OR (SELECT count(DISTINCT "TargetKey") FROM "DataMigrationRecords" WHERE "Source"='SS'
        AND "EntityType"='Shift' AND "LegacyId" IN ('900000','900001','900099')) <> 1
        OR (SELECT count(DISTINCT "PayloadHash") FROM "DataMigrationRecords" WHERE "Source"='SS'
        AND "EntityType"='Shift' AND "LegacyId" IN ('900000','900001','900099')) <> 1 THEN
        RAISE EXCEPTION 'Complete touching-shift membership/hash proof missing.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "DataMigrationRuns"
        WHERE "Id"='79729120-3af5-49eb-a043-6d8a6b10813f' AND "InsertedCount"=0
        AND "UpdatedCount"=11 AND "RejectedCount"=0 AND "FailedCount"=0) THEN
        RAISE EXCEPTION 'Guarded graph replay counts differ.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "Users" u JOIN "DataMigrationRecords" r ON r."TargetKey"=u."Id"::text
        WHERE r."Source"='SS' AND r."EntityType"='User'
        AND r."LegacyId"='11111111-1111-4111-8111-111111111111'
        AND u."TrainingProfileId" IS NOT NULL AND u."LastName"='Synthetic profile receipt revision') THEN
        RAISE EXCEPTION 'Training/MyTeam field ownership composition failed.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "DataMigrationOutcomes"
        WHERE "RunId"='68aa7f9b-ccf1-4900-9958-470fce0aab3c' AND "EntityType"='SheriffTraining'
        AND "LegacyId"='900000' AND "Outcome"='Rejected' AND "Reason"='OwnedTargetDrift')
        OR NOT EXISTS (SELECT 1 FROM "UserTrainings" t JOIN "DataMigrationRecords" r
        ON r."TargetKey"=t."Id"::text WHERE r."Source"='SS' AND r."EntityType"='SheriffTraining'
        AND r."LegacyId"='900000' AND t."Notes"='Synthetic independent application edit' AND t."Version"=1) THEN
        RAISE EXCEPTION 'Independent target drift rejection/preservation proof missing.';
    END IF;
END $$;
SELECT 'Verified named recovery, retry, backfill, mapped replay, composition and adverse availability/drift evidence.' AS "Result";
ROLLBACK;
