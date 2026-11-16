\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
    IF current_database() <> 'ss_ready_v4_probe'
        OR EXISTS (SELECT 1 FROM "User" WHERE "Id"='33333333-3333-4333-8333-333333333333') THEN
        RAISE EXCEPTION 'Recovery probes require the untouched named ready-v4 clone.';
    END IF;
END $$;
INSERT INTO "User" ("Id","CreatedOn","IdirName","Discriminator","IsEnabled","FirstName","LastName",
    "Email","HomeLocationId","Rank","Excused")
VALUES ('33333333-3333-4333-8333-333333333333','2020-01-01Z','synthetic.retry','Sheriff',true,
    'Synthetic','Retry','synthetic.retry@example.invalid',900000,'UNRESOLVED_SYNTHETIC_PROBE',false);
INSERT INTO "SheriffLeave" ("Id","CreatedOn","SheriffId","LeaveTypeId","StartDate","EndDate","Timezone","Comment")
VALUES (900050,'2020-01-01Z','11111111-1111-4111-8111-111111111111',17,
    '2014-06-02T08:00Z','2014-06-02T16:00Z',NULL,'Synthetic earlier-history backfill');
INSERT INTO "SheriffAwayLocation" ("Id","CreatedOn","SheriffId","LocationId","StartDate","EndDate","Timezone","Comment")
VALUES (900051,'2020-01-01Z','33333333-3333-4333-8333-333333333333',900001,
    '2026-11-05T10:00Z','2026-11-05T12:00Z',NULL,'Synthetic dependency retry'),
    (900052,'2020-01-01Z','11111111-1111-4111-8111-111111111111',900001,
    '2026-12-10T10:00Z','2026-12-10T12:00Z',NULL,'Synthetic unapproved future partial-day impact');
COMMIT;
