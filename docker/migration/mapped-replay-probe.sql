\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
    IF current_database() <> 'ss_ready_v4_probe'
        OR shobj_description((SELECT oid FROM pg_database WHERE datname=current_database()),'pg_database')
            IS DISTINCT FROM 'SS SYNTHETIC'
        OR EXISTS (SELECT 1 FROM "Shift" WHERE "Id"=900099) THEN
        RAISE EXCEPTION 'Mapped replay requires the named synthetic probe at its initial scheduling checkpoint.';
    END IF;
END $$;
UPDATE "SheriffTraining" SET "Note"='Synthetic mapped note revision',
    "UpdatedOn"='2026-11-16T18:00Z' WHERE "Id"=900000;
UPDATE "Assignment" SET "Comment"='Synthetic mapped definition revision',
    "UpdatedOn"='2026-11-16T18:00Z' WHERE "Id"=900000;
UPDATE "Shift" SET "Comment"='Synthetic mapped shift revision',
    "UpdatedOn"='2026-11-16T18:00Z' WHERE "Id"=900000;
UPDATE "Duty" SET "Comment"='Synthetic mapped duty revision',
    "UpdatedOn"='2026-11-16T18:00Z' WHERE "Id" IN (900000,900002);
INSERT INTO "Shift" ("Id","CreatedOn","SheriffId","LocationId","StartDate","EndDate","Timezone","OvertimeHours","Comment")
SELECT 900099,'2026-11-16T18:00Z',"SheriffId","LocationId","EndDate","EndDate"+interval '1 hour',
    "Timezone",0,'Synthetic touching group member' FROM "Shift" WHERE "Id"=900001;
COMMIT;
