$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../..'))
$compose = @('compose','-f','docker/migration/compose.yml','-f','docker/migration/adverse-v7.yml')
function Target([string]$sql) {
    $sql | rtk docker exec -i unified-ss-migration-target-1 psql -X -U postgres -d unified_ss_rehearsal_adverse_v7 -v ON_ERROR_STOP=1 -P pager=off
    if ($LASTEXITCODE -ne 0) { throw 'Adverse SQL assertion failed' }
}
Target @'
DO $$ BEGIN IF current_database()<>'unified_ss_rehearsal_adverse_v7'
THEN RAISE EXCEPTION 'Isolated target required'; END IF; END $$;
CREATE TABLE IF NOT EXISTS "SsAdverseV7Proof" ("Id" int PRIMARY KEY,"Digest" text NOT NULL);
ALTER TABLE "SsAdverseV7Proof" ADD COLUMN IF NOT EXISTS "AuditFloor" bigint NOT NULL DEFAULT 0;
CREATE OR REPLACE FUNCTION public.ss_adverse_v7_hash() RETURNS text LANGUAGE plpgsql AS $$
DECLARE t record; value text; items jsonb:='[]'; BEGIN
 FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
  WHERE n.nspname='public' AND c.relkind IN ('r','p')
  AND c.relname NOT IN ('SsAdverseV7Proof','AuditRecords')
  AND (c.relname NOT LIKE 'DataMigration%' OR c.relname IN ('DataMigrationRecords',
  'DataMigrationMutations','DataMigrationWorkItems','DataMigrationCheckpoints','DataMigrationWatermarks'))
  ORDER BY c.relname LOOP
  EXECUTE format('SELECT COALESCE(jsonb_agg(jsonb_build_array(ctid::text,xmin::text)
    ORDER BY ctid),''[]''::jsonb)::text FROM public.%I',t.relname) INTO value;
  items:=items||jsonb_build_array(jsonb_build_array(t.relname,value));
 END LOOP;
 FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
  WHERE n.nspname='public' AND c.relkind='S' AND c.relname NOT LIKE 'DataMigration%'
  AND c.relname NOT LIKE 'AuditRecords%' ORDER BY c.relname LOOP
  EXECUTE format('SELECT jsonb_build_array(last_value,log_cnt,is_called)::text FROM public.%I',t.relname) INTO value;
  items:=items||jsonb_build_array(jsonb_build_array(t.relname,value));
 END LOOP;
 RETURN encode(sha256(convert_to(items::text,'UTF8')),'hex');
END $$;
INSERT INTO "SsAdverseV7Proof" ("Id","Digest","AuditFloor")
VALUES (1,public.ss_adverse_v7_hash(),(SELECT COALESCE(max("Id"),0) FROM "AuditRecords"))
ON CONFLICT ("Id") DO UPDATE SET "Digest"=excluded."Digest","AuditFloor"=excluded."AuditFloor";
'@
foreach ($job in @('MyTeam','Training','Scheduling','Report')) {
    $output=rtk docker @compose run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll $job
    $code=$LASTEXITCODE
    Write-Output "AttemptedJob=$job; ExitCode=$code"
    $output | Select-String '^Run=|rejected|failed|Failed|NeedsAction|prerequisite|Prerequisite'
    if ($code -eq 0) { throw 'Adverse job unexpectedly succeeded; inspect actual evidence' }
    Target @'
DO $$ BEGIN
 IF (SELECT "Digest" FROM "SsAdverseV7Proof" WHERE "Id"=1) IS DISTINCT FROM public.ss_adverse_v7_hash()
 THEN RAISE EXCEPTION 'Dry adverse run changed ordinary/applied footprint'; END IF;
 IF EXISTS(SELECT 1 FROM pg_namespace WHERE nspname LIKE 'ss_sim_%')
 THEN RAISE EXCEPTION 'Adverse simulation schema leaked'; END IF;
 IF EXISTS(SELECT 1 FROM "AuditRecords" WHERE "Id">(SELECT "AuditFloor" FROM "SsAdverseV7Proof" WHERE "Id"=1)
  AND "EntityType" NOT LIKE 'DataMigration%')
 THEN RAISE EXCEPTION 'Adverse business audit escaped rollback'; END IF;
 IF EXISTS(SELECT 1 FROM "DataMigrationSimulations")
 THEN RAISE EXCEPTION 'Failed adverse lineage published simulation candidates'; END IF;
 RAISE NOTICE 'PASS adverse dry-run ordinary/applied/sequence footprint unchanged';
END $$;
'@
}
Target @'
SELECT "Job","Id","Status","ScannedCount","RejectedCount","FailedCount","SkippedCount"
FROM "DataMigrationRuns" ORDER BY "StartedOn";
SELECT o."EntityType",o."Outcome",o."Reason",count(*) AS rows
FROM "DataMigrationOutcomes" o GROUP BY 1,2,3 ORDER BY 1,2,3;
SELECT count(*) AS applied_crosswalk_rows FROM "DataMigrationRecords";
'@
