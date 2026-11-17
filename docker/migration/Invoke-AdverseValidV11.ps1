param(
 [ValidateSet('All','Initialize','Training','Scheduling','Report','ReportWhileRunning','Verify')][string]$Step='All',
 [ValidateSet('11','12','13','16','18','20')][string]$Version='11'
)
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../..'))
$compose = @('compose','-f','docker/migration/compose.yml','-f',("docker/migration/adverse-valid-v"+$Version+".yml"))
$database = 'unified_ss_rehearsal_adverse_valid_v'+$Version
function Target([string]$sql) {
    $sql | rtk docker exec -i unified-ss-migration-target-1 psql -X -q -U postgres -d $database -v ON_ERROR_STOP=1 -P pager=off
    if ($LASTEXITCODE -ne 0) { throw 'V11 PostgreSQL regression failed' }
}
if ($Step -in @('All','Initialize')) { Target @'
DO $$ BEGIN
 IF current_database() NOT IN ('unified_ss_rehearsal_adverse_valid_v11','unified_ss_rehearsal_adverse_valid_v12','unified_ss_rehearsal_adverse_valid_v13','unified_ss_rehearsal_adverse_valid_v16','unified_ss_rehearsal_adverse_valid_v18','unified_ss_rehearsal_adverse_valid_v20')
 OR NOT EXISTS(SELECT 1 FROM "DataMigrationRuns" WHERE "Job"='MyTeam' AND "Status"='Completed')
 OR NOT EXISTS(SELECT 1 FROM "DataMigrationSimulations")
 THEN RAISE EXCEPTION 'Real completed candidate parent is required'; END IF;
END $$;
CREATE TABLE "DataMigrationValidityV11Proof"("Digest" text NOT NULL,"AuditFloor" bigint NOT NULL);
CREATE FUNCTION public.ss_validity_v11_hash() RETURNS text LANGUAGE plpgsql AS $$
DECLARE t record; value text; items jsonb:='[]'; BEGIN
 FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
  WHERE n.nspname='public' AND c.relkind IN ('r','p')
  AND c.relname NOT IN ('DataMigrationValidityV11Proof','AuditRecords')
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
INSERT INTO "DataMigrationValidityV11Proof" SELECT public.ss_validity_v11_hash(),COALESCE(max("Id"),0) FROM "AuditRecords";
'@
}
if ($Step -eq 'ReportWhileRunning') { Target @'
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM "DataMigrationRuns" c JOIN "DataMigrationRuns" p
 ON p."Id"=c."MyTeamRunId" WHERE c."Job"='Scheduling' AND c."Status"='Running'
 AND p."Job"='MyTeam' AND p."Status"='Completed' AND p."DryRun")
 THEN RAISE EXCEPTION 'A genuinely running child of a completed dry parent is required'; END IF;
 IF EXISTS(SELECT 1 FROM "DataMigrationRuns" WHERE "Job"='Report')
 THEN RAISE EXCEPTION 'Report guard fixture already has a Report run'; END IF;
END $$;
'@
}
foreach ($job in @('Training','Scheduling','Report') | Where-Object { $Step -eq 'All' -or $Step -eq $_ -or ($Step -eq 'ReportWhileRunning' -and $_ -eq 'Report') }) {
    $output=rtk docker @compose run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll $job
    Write-Output "AttemptedJob=$job; ExitCode=$LASTEXITCODE"
    $output | Select-String '^Run=|ActualPrivateSchedulingGraph|ActualCaptureQueryShapes|FailureType|SqlState'
    Target @'
DO $$ BEGIN
 IF (SELECT "Digest" FROM "DataMigrationValidityV11Proof") IS DISTINCT FROM public.ss_validity_v11_hash()
 THEN RAISE EXCEPTION 'Ordinary/applied/business-sequence footprint changed'; END IF;
 IF EXISTS(SELECT 1 FROM pg_namespace WHERE nspname LIKE 'ss_sim_%')
 THEN RAISE EXCEPTION 'Private schema escaped rollback'; END IF;
 IF EXISTS(SELECT 1 FROM "AuditRecords" WHERE "Id">(SELECT "AuditFloor" FROM "DataMigrationValidityV11Proof")
 AND "EntityType" NOT LIKE 'DataMigration%')
 THEN RAISE EXCEPTION 'Business audit escaped rollback'; END IF;
 RAISE NOTICE 'PASS valid-parent child rollback physical regression';
END $$;
'@
}
if ($Step -eq 'ReportWhileRunning') { Target @'
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM "DataMigrationRuns" WHERE "Job"='Report')
 THEN RAISE EXCEPTION 'Report acquired incomplete/running lineage'; END IF;
 RAISE NOTICE 'PASS actual Report acquisition blocked under running valid-parent child';
END $$;
'@
}
if ($Step -in @('All','Verify')) { Target @'
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM "DataMigrationRuns" WHERE "Job"='Training' AND "ScannedCount">0)
 OR NOT EXISTS(SELECT 1 FROM "DataMigrationRuns" WHERE "Job"='Scheduling' AND "ScannedCount">0)
 THEN RAISE EXCEPTION 'Dependent business writers did not actually execute'; END IF;
 IF EXISTS(SELECT 1 FROM "DataMigrationRuns" c LEFT JOIN "DataMigrationRuns" p
 ON p."Id"=c."MyTeamRunId" WHERE c."Job" IN ('Training','Scheduling')
 AND (p."Job"<>'MyTeam' OR p."Status"<>'Completed' OR NOT p."DryRun"))
 THEN RAISE EXCEPTION 'Wrong dependent lineage'; END IF;
END $$;
SELECT "Id","Job","Status","ScannedCount","RejectedCount","FailedCount","MyTeamRunId"
FROM "DataMigrationRuns" ORDER BY "StartedOn";
'@
}
