param([switch]$Adverse, [switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../..'))
$compose = @('compose','-f','docker/migration/compose.yml','-f','docker/migration/ready-v4.yml',
    '-f','docker/migration/rollback-ready-v4.yml','-f','docker/migration/repartition-v5.yml')
function Sql([string]$database, [string]$sql) {
    $container = if ($database -eq 'ss_repartition_v5') { 'unified-ss-migration-source-1' } else { 'unified-ss-migration-target-1' }
    if ($database -notin @('ss_repartition_v5','unified_ss_rehearsal_repartition_v5')) { throw 'Isolated database required' }
    $sql | rtk docker exec -i $container psql -U postgres -d $database -v ON_ERROR_STOP=1 -P pager=off
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL assertion/mutation failed' }
}
function Source([string]$sql) { Sql 'ss_repartition_v5' $sql }
function Target([string]$sql) { Sql 'unified_ss_rehearsal_repartition_v5' $sql }
function Run([string]$job = 'Scheduling', [string]$status = 'Completed') {
    $output = rtk docker @compose run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll $job
    $code = $LASTEXITCODE
    $line = $output | Select-String '^Run='
    $line | Write-Output
    if ($null -eq $line -or "$line" -notmatch "; Status=$status;" -or ($status -eq 'Completed' -and $code -ne 0) -or
        ($status -ne 'Completed' -and $code -eq 0)) { throw 'Unexpected migration result' }
}
function Before {
    Target ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'graph-proof-v4.sql')))
    Target @'
CREATE TABLE IF NOT EXISTS "SsGraphRetainProof"("EntityType" text,"LegacyId" text,"PayloadHash" text,"TargetKey" text,"PhysicalHash" text);
TRUNCATE "SsGraphRetainProof";
INSERT INTO "SsGraphRetainProof"
SELECT r."EntityType",r."LegacyId",r."PayloadHash",r."TargetKey",
 CASE r."EntityType"
 WHEN 'Assignment' THEN (SELECT encode(sha256(convert_to(to_jsonb(d)::text,'UTF8')),'hex') FROM "AssignmentDefinitions" d WHERE d."Id"::text=r."TargetKey")
 WHEN 'Shift' THEN (SELECT encode(sha256(convert_to(jsonb_build_object('shift',to_jsonb(s),'event',to_jsonb(e),
 'users',(SELECT jsonb_agg(to_jsonb(u) ORDER BY u."Id") FROM "ShiftEntryUsers" u WHERE u."ShiftEntryId"=s."Id"))::text,'UTF8')),'hex')
 FROM "ShiftEntries" s JOIN "Events" e ON e."Id"=s."EventId" WHERE s."Id"::text=r."TargetKey") END
FROM "DataMigrationRecords" r WHERE r."Source"='SS' AND r."EntityType" IN ('Assignment','Shift','Duty','AssignmentSeries');
'@
}
function Verify([string]$scenario) {
    # The SQL scenario is a fixed caller value, never arbitrary business input.
    Target ("SELECT set_config('ss.probe.scenario','$scenario',false);" +
        ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'verify-repartition-v4.sql')) -replace '(?m)^\\set.*\r?\n','' -replace "SELECT set_config\('ss.probe.scenario', :'scenario', false\);",''))
    Target @'
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM "SsGraphRetainProof" p JOIN "DataMigrationRecords" r
 ON r."Source"='SS' AND r."EntityType"=p."EntityType" AND r."LegacyId"=p."LegacyId"
 WHERE r."PayloadHash"=p."PayloadHash" AND r."TargetKey"<>p."TargetKey")
 THEN RAISE EXCEPTION 'Unchanged mapped source root physically replaced'; END IF;
 IF EXISTS(SELECT 1 FROM "SsGraphRetainProof" p JOIN "DataMigrationRecords" r
 ON r."Source"='SS' AND r."EntityType"=p."EntityType" AND r."LegacyId"=p."LegacyId"
 WHERE r."PayloadHash"=p."PayloadHash" AND p."PhysicalHash" IS DISTINCT FROM
 CASE p."EntityType"
 WHEN 'Assignment' THEN (SELECT encode(sha256(convert_to(to_jsonb(d)::text,'UTF8')),'hex') FROM "AssignmentDefinitions" d WHERE d."Id"::text=r."TargetKey")
 WHEN 'Shift' THEN (SELECT encode(sha256(convert_to(jsonb_build_object('shift',to_jsonb(s),'event',to_jsonb(e),
 'users',(SELECT jsonb_agg(to_jsonb(u) ORDER BY u."Id") FROM "ShiftEntryUsers" u WHERE u."ShiftEntryId"=s."Id"))::text,'UTF8')),'hex')
 FROM "ShiftEntries" s JOIN "Events" e ON e."Id"=s."EventId" WHERE s."Id"::text=r."TargetKey") END)
 THEN RAISE EXCEPTION 'Unchanged definition/shift row edited'; END IF;
 RAISE NOTICE 'PASS unchanged source roots retain IDs; unchanged definitions/shifts retain complete rows';
END $$;
'@
}
function Unchanged {
    Target @'
DO $$ BEGIN
 IF (SELECT "Digest" FROM "SsGraphProbeProof" WHERE "Key"='before') IS DISTINCT FROM public.ss_probe_scheduling_hash()
 THEN RAISE EXCEPTION 'Business footprint changed'; END IF;
 RAISE NOTICE 'PASS exact scheduling footprint unchanged';
END $$;
'@
}
if ($VerifyOnly) {
    Before
    Verify 'shift-split'
    return
}
if ($Adverse) {
    Before
    Target @'
CREATE FUNCTION public.ss_probe_inject_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION USING ERRCODE='40001',MESSAGE='Synthetic injected serialization failure'; END $$;
CREATE TRIGGER ss_probe_inject_failure BEFORE INSERT ON "AssignmentEntries" FOR EACH ROW EXECUTE FUNCTION public.ss_probe_inject_failure();
'@
    Source 'UPDATE "Shift" SET "StartDate"=''2026-11-02T13:00:00Z'',"UpdatedOn"=now() WHERE "Id"=900001;'
    try { Run 'Scheduling' 'Failed'; Unchanged } finally {
        Target 'DROP TRIGGER ss_probe_inject_failure ON "AssignmentEntries"; DROP FUNCTION public.ss_probe_inject_failure();'
    }
    Run
    Verify 'shift-split'
    Target 'UPDATE "Events" SET "Description"=''Independent target edit'' WHERE "Id"=(SELECT a."EventId" FROM "AssignmentEntries" a JOIN "DataMigrationRecords" r ON r."TargetKey"=a."Id"::text WHERE r."EntityType"=''Duty'' AND r."LegacyId"=''900000'');'
    Before
    Source 'UPDATE "Shift" SET "StartDate"=''2026-11-02T12:00:00Z'',"UpdatedOn"=now() WHERE "Id"=900001;'
    Run 'Scheduling' 'Failed'
    Unchanged
    return
}
Before
Run
Unchanged
Before
Source 'UPDATE "Shift" SET "StartDate"=''2026-11-02T13:00:00Z'',"UpdatedOn"=now() WHERE "Id"=900001;'
Run
Verify 'shift-split'
Before
Source 'UPDATE "Shift" SET "StartDate"=''2026-11-02T12:00:00Z'',"UpdatedOn"=now() WHERE "Id"=900001;'
Run
Verify 'shift-merge'
Before
Source 'UPDATE "Assignment" SET ("Name","LookupCodeId","LocationId")=(SELECT "Name","LookupCodeId","LocationId" FROM "Assignment" WHERE "Id"=900000),"UpdatedOn"=now() WHERE "Id"=900010;'
Run
Verify 'definition-merge'
Before
Source 'UPDATE "Assignment" SET "Name"=''Repartition split definition'',"UpdatedOn"=now() WHERE "Id"=900010;'
Run
Verify 'definition-split'
Before
Source 'UPDATE "Assignment" SET "Monday"=false,"Tuesday"=false,"Wednesday"=false,"Thursday"=false,"Friday"=false,"Saturday"=false,"Sunday"=false,"UpdatedOn"=now() WHERE "Id"=900010;'
Run
Verify 'series-fallback'
Before
Source 'UPDATE "Assignment" SET "Monday"=true,"Tuesday"=true,"UpdatedOn"=now() WHERE "Id"=900010;'
Run
Verify 'series-qualified'
Source @'
INSERT INTO "User" SELECT (jsonb_populate_record(NULL::"User",to_jsonb(u)||jsonb_build_object(
'Id','33333333-3333-4333-8333-333333333333','IdirName','ss-repartition-second','Email','ss-repartition-second@example.invalid',
'IdirId',NULL,'KeyCloakId',NULL,'Photo',NULL,'LastPhotoUpdate',NULL,'CreatedOn',now(),'UpdatedOn',now()))).*
FROM "User" u WHERE u."Id"=(SELECT "SheriffId" FROM "Shift" WHERE "Id"=900000);
'@
Run 'MyTeam'
Before
Source @'
INSERT INTO "Shift" SELECT (jsonb_populate_record(NULL::"Shift",to_jsonb(s)||jsonb_build_object(
'Id',900050,'SheriffId','33333333-3333-4333-8333-333333333333','EndDate','2026-11-02T16:00:00Z','CreatedOn',now(),'UpdatedOn',now()))).*
FROM "Shift" s WHERE s."Id"=900000;
INSERT INTO "DutySlot" SELECT (jsonb_populate_record(NULL::"DutySlot",to_jsonb(s)||jsonb_build_object(
'Id',900050,'SheriffId','33333333-3333-4333-8333-333333333333','EndDate','2026-11-02T16:00:00Z','CreatedOn',now(),'UpdatedOn',now()))).*
FROM "DutySlot" s WHERE s."Id"=900000;
'@
Run
Verify 'duty-multi'
Before
Source 'UPDATE "DutySlot" SET "EndDate"=''2026-11-02T14:00:00Z'',"UpdatedOn"=now() WHERE "Id"=900050;'
Run
Verify 'duty-multi-update'
Before
Source 'UPDATE "DutySlot" SET "ExpiryDate"=now(),"UpdatedOn"=now() WHERE "Id"=900050;'
Run
Verify 'duty-single'
Before
Source @'
INSERT INTO "Duty" SELECT (jsonb_populate_record(NULL::"Duty",to_jsonb(s)||jsonb_build_object(
'Id',900012,'StartDate','2027-01-06T08:00:00Z','EndDate','2027-01-06T16:00:00Z','CreatedOn',now(),'UpdatedOn',now()))).*
FROM "Duty" s WHERE s."Id"=900010;
INSERT INTO "Shift" SELECT (jsonb_populate_record(NULL::"Shift",to_jsonb(s)||jsonb_build_object(
'Id',900012,'StartDate','2027-01-06T08:00:00Z','EndDate','2027-01-06T16:00:00Z','CreatedOn',now(),'UpdatedOn',now()))).*
FROM "Shift" s WHERE s."Id"=900010;
INSERT INTO "DutySlot" SELECT (jsonb_populate_record(NULL::"DutySlot",to_jsonb(s)||jsonb_build_object(
'Id',900012,'DutyId',900012,'StartDate','2027-01-06T08:00:00Z','EndDate','2027-01-06T16:00:00Z','CreatedOn',now(),'UpdatedOn',now()))).*
FROM "DutySlot" s WHERE s."Id"=900010;
UPDATE "Assignment" SET "Wednesday"=true,"UpdatedOn"=now() WHERE "Id"=900010;
'@
Run
Verify 'series-three'
Before
Source 'UPDATE "Assignment" SET "Tuesday"=false,"UpdatedOn"=now() WHERE "Id"=900010;'
Run
Verify 'series-two-selected'
Before
Run
Unchanged
