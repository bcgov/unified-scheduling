param(
 [switch]$AfterBaseline,
 [switch]$AfterFallbackFailure,
 [ValidateSet('api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll',
 '/tmp/ss-series17-fixed/Unified.DataMigration.Runner.dll')]
 [string]$RunnerDll='api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll'
)
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../..'))
$compose = @('compose','-f','docker/migration/compose.yml','-f','docker/migration/ready-v4.yml',
 '-f','docker/migration/rollback-ready-v4.yml','-f','docker/migration/series-v17.yml')
function Sql([string]$side, [string]$sql) {
 $database = if ($side -eq 'source') {'ss_series_v17'} else {'unified_ss_rehearsal_series_v17'}
 $sql | rtk docker exec -i ("unified-ss-migration-"+$side+"-1") psql -X -q -U postgres -d $database -v ON_ERROR_STOP=1 -P pager=off
 if ($LASTEXITCODE -ne 0) {throw 'Isolated series PG assertion failed'}
}
function Target([string]$sql) {Sql 'target' $sql}
function Source([string]$sql) {Sql 'source' $sql}
function Run([string]$status='Completed') {
 # The alternate output exists only within an explicitly supplied tools container.
 $output = if ($RunnerDll.StartsWith('/tmp/')) {
  rtk docker @compose run --rm --no-deps --entrypoint sh runner -c 'dotnet build api/Unified.DataMigration.Runner/Unified.DataMigration.Runner.csproj --no-restore -o /tmp/ss-series17-fixed -clp:ErrorsOnly && dotnet /tmp/ss-series17-fixed/Unified.DataMigration.Runner.dll Scheduling'
 } else {
  rtk docker @compose run --rm --no-deps --entrypoint dotnet runner $RunnerDll Scheduling
 }
 $code = $LASTEXITCODE
 $line = $output | Select-String '^Run='
 $line | Write-Output
 if ($null -eq $line -or "$line" -notmatch "; Status=$status;" -or
 ($status -eq 'Completed' -and $code -ne 0) -or ($status -eq 'Failed' -and $code -eq 0)) {throw 'Unexpected series run result'}
}
function Verify([string]$mode) {
 Target @"
DO `$`$ BEGIN
 IF (SELECT count(*) FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='Duty'
 AND "LegacyId" IN ('900010','900011'))<>2
 OR (SELECT count(DISTINCT "TargetKey") FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='Duty'
 AND "LegacyId" IN ('900010','900011'))<>2 THEN RAISE EXCEPTION 'Real occurrence roots missing or duplicated'; END IF;
 IF EXISTS(SELECT 1 FROM "DataMigrationRecords" r LEFT JOIN "AssignmentEntries" e ON e."Id"::text=r."TargetKey"
 WHERE r."EntityType" IN ('Duty','DutySlot','DutyEntry') AND e."Id" IS NULL)
 OR EXISTS(SELECT 1 FROM "Events" e WHERE e."EventTypeCode"='assignment'
 AND NOT EXISTS(SELECT 1 FROM "AssignmentEntries" a WHERE a."EventId"=e."Id"))
 OR EXISTS(SELECT 1 FROM "EventSeries" e WHERE e."EventTypeCode"='assignment'
 AND NOT EXISTS(SELECT 1 FROM "AssignmentSeries" a WHERE a."EventSeriesId"=e."Id"))
 THEN RAISE EXCEPTION 'Orphan live root/crosswalk'; END IF;
 IF '$mode'='standalone' THEN
  IF EXISTS(SELECT 1 FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='AssignmentSeries' AND "LegacyId"='g10:assignment:900010')
  OR EXISTS(SELECT 1 FROM "AssignmentEntries" e JOIN "DataMigrationRecords" r ON r."TargetKey"=e."Id"::text
   WHERE r."EntityType"='Duty' AND r."LegacyId" IN ('900010','900011') AND e."AssignmentSeriesId" IS NOT NULL)
  OR NOT EXISTS(SELECT 1 FROM "DataMigrationOutcomes" WHERE "RunId"=(SELECT "Id" FROM "DataMigrationRuns"
   WHERE "Job"='Scheduling' ORDER BY "StartedOn" DESC LIMIT 1) AND "EntityType"='AssignmentSeries'
   AND "Outcome"='Excluded' AND "Reason"='StandaloneInconsistentMember')
  THEN RAISE EXCEPTION 'Mixed capacities did not safely remain standalone'; END IF;
 ELSE
  IF (SELECT count(*) FROM "DataMigrationRecords" WHERE "Source"='SS' AND "EntityType"='AssignmentSeries' AND "LegacyId"='g10:assignment:900010')<>1
  OR (SELECT count(*) FROM "AssignmentEntries" e JOIN "DataMigrationRecords" r ON r."TargetKey"=e."Id"::text
   JOIN "DataMigrationRecords" s ON s."EntityType"='AssignmentSeries' AND s."LegacyId"='g10:assignment:900010' AND s."TargetKey"=e."AssignmentSeriesId"::text
   WHERE r."EntityType"='Duty' AND r."LegacyId" IN ('900010','900011'))<>2
  THEN RAISE EXCEPTION 'Actual two-occurrence series not reconstructed'; END IF;
 END IF;
 RAISE NOTICE 'PASS actual $mode series; exact real members; no orphans or crosswalk collisions';
END `$`$;
"@
}
if (-not ($AfterBaseline -or $AfterFallbackFailure)) { Target @'
DO $$ BEGIN
 IF current_database()<>'unified_ss_rehearsal_series_v17'
 OR EXISTS(SELECT 1 FROM "DataMigrationRuns" WHERE "Job"='Scheduling')
 THEN RAISE EXCEPTION 'Fresh isolated series target required'; END IF;
END $$;
'@
Source 'UPDATE "DutySlot" SET "IsClosed"=true,"UpdatedOn"=now() WHERE "DutyId"=900010;'
Run
}
elseif ($AfterBaseline) { Target @'
DO $$ BEGIN
 IF current_database()<>'unified_ss_rehearsal_series_v17'
 OR (SELECT count(*) FROM "DataMigrationRuns" WHERE "Job"='Scheduling')<>1
 OR NOT EXISTS(SELECT 1 FROM "DataMigrationRuns" WHERE "Job"='Scheduling' AND "Status"='Completed'
 AND "RejectedCount"=0 AND "FailedCount"=0)
 THEN RAISE EXCEPTION 'Only the retained initial successful baseline may resume'; END IF;
END $$;
'@ }
else { Target @'
DO $$ BEGIN
 IF current_database()<>'unified_ss_rehearsal_series_v17'
 OR (SELECT count(*) FROM "DataMigrationRuns" WHERE "Job"='Scheduling')<>3
 OR NOT EXISTS(SELECT 1 FROM "DataMigrationOutcomes" WHERE "RunId"=(SELECT "Id" FROM "DataMigrationRuns"
 WHERE "Job"='Scheduling' ORDER BY "StartedOn" DESC LIMIT 1) AND "Reason"='GraphUnresolvedLiveSlot')
 THEN RAISE EXCEPTION 'Only the retained measured fallback failure may resume'; END IF;
END $$;
'@ }
if (-not $AfterFallbackFailure) {
Verify 'standalone'
Source 'UPDATE "DutySlot" SET "IsClosed"=false,"UpdatedOn"=now() WHERE "DutyId"=900010;'
Run
Verify 'qualified'
}
Source 'UPDATE "DutySlot" SET "IsClosed"=true,"UpdatedOn"=now() WHERE "DutyId"=900010;'
Run
Verify 'standalone'
Target 'UPDATE "AssignmentEntries" SET "Capacity"=73 WHERE "Id"=(SELECT "TargetKey"::int FROM "DataMigrationRecords" WHERE "Source"=''SS'' AND "EntityType"=''Duty'' AND "LegacyId"=''900010'');'
Target ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'graph-proof-v4.sql')))
Source 'UPDATE "DutySlot" SET "IsClosed"=false,"UpdatedOn"=now() WHERE "DutyId"=900010;'
Run 'Failed'
Target @'
DO $$ BEGIN
 IF (SELECT "Digest" FROM "SsGraphProbeProof" WHERE "Key"='before') IS DISTINCT FROM public.ss_probe_scheduling_hash()
 THEN RAISE EXCEPTION 'External target edit overwritten or partial graph mutation'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "DataMigrationOutcomes" WHERE "RunId"=(SELECT "Id" FROM "DataMigrationRuns"
 WHERE "Job"='Scheduling' ORDER BY "StartedOn" DESC LIMIT 1) AND "Outcome"='Rejected' AND "Reason"='OwnedTargetDrift')
 THEN RAISE EXCEPTION 'External edit was not rejected by QX4'; END IF;
 RAISE NOTICE 'PASS actual target drift retained; whole graph footprint unchanged';
END $$;
'@
