\set ON_ERROR_STOP on
-- Apply once to a NEW clone of ss_ready_v3 named ss_ready_v4. Preserve all prior fixtures.
BEGIN;
DO $$
BEGIN
    IF current_database() <> 'ss_ready_v4'
        OR (SELECT count(*) FROM "Shift") <> 5
        OR (SELECT count(*) FROM "Duty") <> 4
        OR EXISTS (SELECT 1 FROM "Assignment" WHERE "Id" = 900010) THEN
        RAISE EXCEPTION 'Future extension requires the unextended named ready-v3 clone.';
    END IF;
END $$;
COMMENT ON DATABASE ss_ready_v4 IS 'SS SYNTHETIC';
-- Distinct definition: do not invent a recurrence across the missing December dates.
INSERT INTO "Assignment" ("Id","CreatedOn","LookupCodeId","LocationId","Name","Timezone",
    "Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday","Start","End","Comment")
VALUES (900010,'2020-01-01Z',900000,900000,'Ready January','Etc/UTC',
    true,true,true,true,true,false,false,'08:00','16:00','Synthetic January definition'),
    -- Catalogue-only singleton must retain these defaults even with no observed duties.
    (900020,'2020-01-01Z',900000,900000,'Ready Catalogue Defaults','Etc/UTC',
    true,true,true,true,true,false,false,'09:00','17:00','Synthetic catalogue-only definition');
INSERT INTO "Duty" ("Id","CreatedOn","AssignmentId","LocationId","StartDate","EndDate","Timezone","Comment")
SELECT id,'2020-01-01Z'::timestamptz,900010,900000,
    (d+time '08:00') AT TIME ZONE 'Etc/UTC',(d+time '16:00') AT TIME ZONE 'Etc/UTC',
    'Etc/UTC','Synthetic January duty'
FROM (VALUES (900010,date '2027-01-04'),(900011,date '2027-01-05')) AS t(id,d);
INSERT INTO "Shift" ("Id","CreatedOn","SheriffId","LocationId","StartDate","EndDate","Timezone","OvertimeHours","Comment")
SELECT "Id",'2020-01-01Z','11111111-1111-4111-8111-111111111111',"LocationId",
    "StartDate","EndDate",'Etc/UTC',0,'Synthetic January shift'
FROM "Duty" WHERE "Id" IN (900010,900011);
INSERT INTO "DutySlot" ("Id","CreatedOn","DutyId","LocationId","SheriffId","StartDate","EndDate","Timezone",
    "IsNotAvailable","IsNotRequired","IsClosed","IsOvertime")
SELECT "Id",'2020-01-01Z',"Id","LocationId",'11111111-1111-4111-8111-111111111111',
    "StartDate","EndDate",'Etc/UTC',false,false,false,false
FROM "Duty" WHERE "Id" IN (900010,900011);
COMMIT;
