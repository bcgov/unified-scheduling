\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
    IF current_database() <> 'ss_ready_v4_probe'
        OR shobj_description((SELECT oid FROM pg_database WHERE datname=current_database()),'pg_database')
            IS DISTINCT FROM 'SS SYNTHETIC'
        OR EXISTS (SELECT 1 FROM "LookupCode" WHERE "Id"=900060) THEN
        RAISE EXCEPTION 'Profile composition requires the named synthetic probe before its Carbine extension.';
    END IF;
END $$;
INSERT INTO "LookupCode" ("Id","CreatedOn","Type","Code","Description","Mandatory","Rotating","ValidityPeriod","AdvanceNotice","Category")
VALUES (900060,'2026-11-16T19:00Z',6,'Carbine Initial Course','Synthetic profile composition',true,true,365,30,'Provincial Standard');
INSERT INTO "SheriffTraining" ("Id","CreatedOn","SheriffId","TrainingTypeId","StartDate","EndDate","Timezone","FirstNotice")
VALUES (900060,'2026-11-16T19:00Z','11111111-1111-4111-8111-111111111111',900060,
    '2026-11-01T10:00Z','2026-11-01T10:00Z',NULL,false);
COMMIT;
