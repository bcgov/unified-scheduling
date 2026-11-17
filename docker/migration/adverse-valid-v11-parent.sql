-- NEW physical template clone only. Original ss and failed target are never repaired.
-- All downstream source rows and the entire original manifest stay intact.
BEGIN;
DO $$ BEGIN
 IF current_database()<>'ss_adverse_valid_v11'
 OR shobj_description((SELECT oid FROM pg_database WHERE datname=current_database()),'pg_database')<>'SS SYNTHETIC'
 THEN RAISE EXCEPTION 'Exact marked new clone required'; END IF;
 IF to_regclass('synthetic.valid_parent_repairs') IS NOT NULL
 THEN RAISE EXCEPTION 'Parent repair is not repeatable'; END IF;
END $$;
CREATE TABLE synthetic.valid_parent_repairs(source_table text,source_id text,reason text);
INSERT INTO synthetic.valid_parent_repairs
SELECT source_table,source_id,'ParentOnlyCloneRepair' FROM synthetic.case_manifest
WHERE case_id IN ('C-REG-01','C-QL6-01','C-QL4-01','C-T7-02')
AND source_table IN ('Region','LookupCode','User');
UPDATE "Region" r SET "Name"='Valid parent region '||r."Id"::text
WHERE EXISTS(SELECT 1 FROM synthetic.valid_parent_repairs p
WHERE p.source_table='Region' AND p.source_id=r."Id"::text);
UPDATE "LookupCode" c SET "Code"=CASE m.case_id
 WHEN 'C-QL6-01' THEN 'ATT Instructor' ELSE 'All Day Medical' END,
 "LocationId"=NULL,"SubCode"=NULL
FROM synthetic.case_manifest m WHERE m.source_table='LookupCode'
AND m.source_id=c."Id"::text AND m.case_id IN ('C-QL6-01','C-QL4-01');
UPDATE "User" u SET "Rank"=(SELECT c."Description" FROM "LookupCode" c
 WHERE c."Type"=7 AND c."Description" IS NOT NULL
 AND (SELECT count(*) FROM "LookupCode" x WHERE x."Type"=7
 AND x."Description"=c."Description")=1
 AND NOT EXISTS(SELECT 1 FROM "LookupCode" x WHERE x."Type"=7 AND x."Code"=c."Description")
 ORDER BY c."Id" LIMIT 1)
WHERE EXISTS(SELECT 1 FROM synthetic.valid_parent_repairs p
WHERE p.source_table='User' AND p.source_id=u."Id"::text);
DO $$ BEGIN
 IF (SELECT count(*) FROM synthetic.case_manifest)<>3388
 OR (SELECT count(*) FROM synthetic.valid_parent_repairs)<>5
 THEN RAISE EXCEPTION 'Original manifest/parent repair inventory differs'; END IF;
END $$;
GRANT SELECT ON synthetic.valid_parent_repairs TO ss_migration_reader;
COMMIT;
