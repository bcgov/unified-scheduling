\set ON_ERROR_STOP on
-- Small, separately named happy-path smoke fixture. NOT a production-shape fixture.
-- Run only against a fresh ss_ready database created from ss-schema-noseed.sql.
-- The adverse ss database and its manifest must never be repaired or replaced with this fixture.
BEGIN;
DO $$
BEGIN
    IF current_database() NOT IN ('ss_ready','ss_ready_v2') OR EXISTS (SELECT 1 FROM "Shift")
        OR EXISTS (SELECT 1 FROM "User" WHERE "Id" <> '00000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'Ready fixture requires a fresh, separately named ss_ready database.';
    END IF;
END $$;
-- Deliberate ready-fixture role selection; the adverse fixture retains its missing-standard-role case.
UPDATE "Role" SET "Name" = 'SuperAdmin' WHERE "Id" = 1;
INSERT INTO "Region" ("Id","JustinId","Code","Name","CreatedOn")
VALUES (900000,900000,'UNUSED','Synthetic Ready Region','2020-01-01Z');
INSERT INTO "Location" ("Id","AgencyId","Name","RegionId","Timezone","CreatedOn")
VALUES (900000,'SYN-READY','Synthetic Ready Court',900000,'America/Vancouver','2020-01-01Z');
INSERT INTO "User" ("Id","CreatedOn","IdirName","Discriminator","IsEnabled","FirstName","LastName","Email","HomeLocationId","Rank","Excused")
VALUES ('11111111-1111-4111-8111-111111111111','2020-01-01Z',' synthetic.ready ','Sheriff',true,
    'Synthetic','Ready','synthetic.ready@example.invalid',900000,'Deputy Sheriff',false);
INSERT INTO "UserRole" ("Id","CreatedOn","UserId","RoleId","EffectiveDate","ExpiryDate","ExpiryReason")
VALUES (900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111',3,'2010-01-01Z','2030-01-01Z','Synthetic fixture');
INSERT INTO "LookupCode" ("Id","CreatedOn","Type","Code","Description","LocationId","Mandatory","ValidityPeriod","AdvanceNotice","Rotating")
VALUES (900000,'2020-01-01Z',0,'Ready Room','Synthetic ready room',900000,false,0,0,false);
INSERT INTO "Assignment" ("Id","CreatedOn","LookupCodeId","LocationId","Name","Timezone",
    "Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday","Start","End","Comment")
VALUES (900000,'2020-01-01Z',900000,900000,'Ready','America/Vancouver',
    true,true,true,true,true,false,false,'08:00','16:00','Synthetic definition description');
INSERT INTO "Duty" ("Id","CreatedOn","AssignmentId","LocationId","StartDate","EndDate","Timezone","Comment")
SELECT id,'2020-01-01Z'::timestamptz,900000,900000,(d+time '08:00') AT TIME ZONE 'America/Vancouver',
    (d+time '16:00') AT TIME ZONE 'America/Vancouver','America/Vancouver','Synthetic duty description'
FROM (VALUES (900000,date '2026-11-02'),(900001,date '2026-12-01'),(900002,date '2026-12-02')) AS t(id,d);
INSERT INTO "Shift" ("Id","CreatedOn","SheriffId","LocationId","StartDate","EndDate","Timezone","OvertimeHours","Comment")
SELECT id,'2020-01-01Z'::timestamptz,'11111111-1111-4111-8111-111111111111'::uuid,900000,
    s,e,'America/Vancouver',0,'Synthetic shift description'
FROM (VALUES
    (900000,'2026-11-02T16:00Z'::timestamptz,'2026-11-02T20:00Z'::timestamptz),
    (900001,'2026-11-02T20:00Z'::timestamptz,'2026-11-03T00:00Z'::timestamptz),
    (900002,'2026-12-01T16:00Z'::timestamptz,'2026-12-02T00:00Z'::timestamptz),
    (900003,'2026-12-02T16:00Z'::timestamptz,'2026-12-03T00:00Z'::timestamptz)) AS t(id,s,e);
INSERT INTO "DutySlot" ("Id","CreatedOn","DutyId","LocationId","SheriffId","StartDate","EndDate","Timezone",
    "IsNotAvailable","IsNotRequired","IsClosed","IsOvertime")
SELECT "Id",'2020-01-01Z', "Id",900000,'11111111-1111-4111-8111-111111111111',"StartDate","EndDate",
    'America/Vancouver',false,false,false,false FROM "Duty";
INSERT INTO "SheriffTraining" ("Id","CreatedOn","SheriffId","TrainingTypeId","StartDate","EndDate",
    "TrainingCertificationExpiry","Timezone","FirstNotice","Comment","Note")
VALUES (900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111',16,'2026-11-01T10:00Z','2026-11-01T10:00Z',
    NULL,NULL,true,'Synthetic other completion','Distinct synthetic note');
INSERT INTO "SheriffLeave" ("Id","CreatedOn","SheriffId","LeaveTypeId","StartDate","EndDate","Timezone","Comment")
VALUES (900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111',17,'2026-11-04T16:00Z','2026-11-05T00:00Z',
    NULL,'Synthetic leave comment');
INSERT INTO "SheriffActingRank" ("Id","CreatedOn","SheriffId","Rank","StartDate","EndDate","Timezone","Comment")
VALUES (900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','Sergeant','2026-11-06T16:00Z',
    '2026-11-07T00:00Z',NULL,'Synthetic acting comment');
SELECT format('GRANT CONNECT ON DATABASE %I TO ss_migration_reader',current_database()) \gexec
GRANT USAGE ON SCHEMA public TO ss_migration_reader;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO ss_migration_reader;
COMMIT;
