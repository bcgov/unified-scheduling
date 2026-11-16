$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../..'))
$compose = @('compose','-f','docker/migration/compose.yml','-f','docker/migration/ready-v4.yml',
    '-f','docker/migration/rollback-ready-v4.yml','-f','docker/migration/simulation-v6.yml')
function Sql([string]$db, [string]$sql) {
    if ($db -notin @('ss_simulation_v6','unified_ss_rehearsal_simulation_v6')) { throw 'Isolated fixture required' }
    $container = if ($db -eq 'ss_simulation_v6') {'unified-ss-migration-source-1'} else {'unified-ss-migration-target-1'}
    $sql | rtk docker exec -i $container psql -U postgres -d $db -v ON_ERROR_STOP=1 -P pager=off
    if ($LASTEXITCODE -ne 0) { throw 'Simulation PostgreSQL assertion failed' }
}
function Target([string]$sql) { Sql 'unified_ss_rehearsal_simulation_v6' $sql }
function Source([string]$sql) { Sql 'ss_simulation_v6' $sql }
function Run([string]$job,[string]$status='Completed',[string]$reason='') {
    $output=rtk docker @compose run --rm --entrypoint dotnet runner api/Unified.DataMigration.Runner/bin/Debug/net10.0/Unified.DataMigration.Runner.dll $job
    $code=$LASTEXITCODE
    $line=$output | Select-String '^Run='
    $line | Write-Output
    if ($null -eq $line -or "$line" -notmatch "; Status=$status;" -or
        ($status -eq 'Completed' -and $code -ne 0) -or ($status -ne 'Completed' -and $code -eq 0)) { throw 'Unexpected simulation verdict' }
    if ($reason -ne '') {
        Target ("DO " + '$$' + " BEGIN IF NOT EXISTS(SELECT 1 FROM `"DataMigrationOutcomes`" WHERE `"RunId`"=" +
            "(SELECT `"Id`" FROM `"DataMigrationRuns`" WHERE `"Job`"='$job' AND `"DryRun`" ORDER BY `"StartedOn`" DESC LIMIT 1)" +
            " AND `"Reason`"='$reason') THEN RAISE EXCEPTION 'Missing expected simulation rejection'; END IF; END " + '$$;')
    }
}
function SaveProof { Target 'INSERT INTO "SsSimulationProbeProof" ("Id","Digest","AuditFloor") VALUES (1,public.ss_simulation_probe_hash(),(SELECT COALESCE(max("Id"),0) FROM "AuditRecords")) ON CONFLICT ("Id") DO UPDATE SET "Digest"=excluded."Digest","AuditFloor"=excluded."AuditFloor";' }
function CheckProof {
    Target @'
DO $$ BEGIN
 IF (SELECT "Digest" FROM "SsSimulationProbeProof" WHERE "Id"=1) IS DISTINCT FROM public.ss_simulation_probe_hash()
 THEN RAISE EXCEPTION 'Ordinary rows/applied links/progress/sequences changed'; END IF;
 IF EXISTS(SELECT 1 FROM pg_namespace WHERE nspname LIKE 'ss_sim_%')
 THEN RAISE EXCEPTION 'Rollback schema leaked'; END IF;
 IF EXISTS(SELECT 1 FROM "AuditRecords" WHERE "Id">(SELECT "AuditFloor" FROM "SsSimulationProbeProof" WHERE "Id"=1)
  AND "EntityType" NOT LIKE 'DataMigration%')
 THEN RAISE EXCEPTION 'Business audit escaped rollback'; END IF;
 IF EXISTS(SELECT 1 FROM "DataMigrationSimulations" s JOIN "DataMigrationRuns" r ON r."Id"=s."RunId"
 WHERE NOT r."DryRun" OR r."Status"<>'Completed')
 THEN RAISE EXCEPTION 'Failed/applied run exposed simulation candidates'; END IF;
 RAISE NOTICE 'PASS all ordinary rows, applied crosswalks, progress and sequences unchanged; no schema leak';
END $$;
'@
}
Target @'
DO $$ BEGIN IF current_database()<>'unified_ss_rehearsal_simulation_v6' THEN RAISE EXCEPTION 'Isolated fixture required'; END IF; END $$;
INSERT INTO "Regions" ("Id","JustinId","Name","CreatedOn") VALUES (777001,777001,'Simulation applied sentinel','2026-01-01T00:00:00Z') ON CONFLICT DO NOTHING;
INSERT INTO "DataMigrationRuns" ("Id","Source","Job","Status","DryRun","CutoverDate","HistoryStartDate","StartedOn",
 "CommittedBatchCount","ScannedCount","InsertedCount","UpdatedCount","UnchangedCount","FailedCount","SkippedCount","RejectedCount")
VALUES ('77777777-7777-4777-8777-777777777777','SS','MyTeam','Completed',false,'2026-12-01','2015-01-01','2026-01-01T00:00:00Z',0,1,1,0,0,0,0,0) ON CONFLICT DO NOTHING;
INSERT INTO "DataMigrationRecords" ("Source","EntityType","LegacyId","TargetType","TargetKey","PayloadHash","CreatedByRunId","LastAppliedRunId","IsDeleted")
VALUES ('SS','Region','applied-sentinel','Region','777001',repeat('A',64),'77777777-7777-4777-8777-777777777777','77777777-7777-4777-8777-777777777777',false) ON CONFLICT DO NOTHING;
CREATE TABLE IF NOT EXISTS "SsSimulationProbeProof" ("Id" int PRIMARY KEY,"Digest" text NOT NULL);
ALTER TABLE "SsSimulationProbeProof" ADD COLUMN IF NOT EXISTS "AuditFloor" bigint NOT NULL DEFAULT 0;
CREATE OR REPLACE FUNCTION public.ss_simulation_probe_hash() RETURNS text LANGUAGE plpgsql AS $$
DECLARE t record; value text; items jsonb:='[]'; BEGIN
 FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
  WHERE n.nspname='public' AND c.relkind IN ('r','p') AND c.relname NOT IN ('SsSimulationProbeProof','AuditRecords')
  AND (c.relname NOT LIKE 'DataMigration%' OR c.relname IN ('DataMigrationRecords','DataMigrationMutations',
    'DataMigrationWorkItems','DataMigrationCheckpoints','DataMigrationWatermarks')) ORDER BY c.relname LOOP
  -- Complete physical identity/version footprint; NO personal text, payload or photo hashes.
  EXECUTE format('SELECT COALESCE(jsonb_agg(jsonb_build_array(ctid::text,xmin::text) ORDER BY ctid),''[]''::jsonb)::text FROM public.%I',t.relname) INTO value;
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
'@
SaveProof
foreach ($job in @('MyTeam','Training','Scheduling','Training','Scheduling')) { Run $job; CheckProof }
Run 'Report' 'NeedsAction' 'SimulationOnlyNotAppliedLineage'
CheckProof
foreach ($job in @('MyTeam','Training','Scheduling')) { Run $job; CheckProof }
Target @'
DO $$ BEGIN
 IF EXISTS(SELECT r."Job" FROM "DataMigrationSimulations" s JOIN "DataMigrationRuns" r ON r."Id"=s."RunId"
  WHERE s."FormatVersion"=2 AND s."ConfigurationHash"=(SELECT z."ConfigurationHash" FROM "DataMigrationSimulations" z
    JOIN "DataMigrationRuns" q ON q."Id"=z."RunId" ORDER BY q."StartedOn" DESC LIMIT 1)
  GROUP BY r."Job" HAVING count(DISTINCT s."CandidateHash")<>1 OR count(DISTINCT s."NamespaceId")<>1)
 THEN RAISE EXCEPTION 'Repeated identical candidate identity/hash namespace changed'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "DataMigrationSimulationCandidates" WHERE "EntityType"='User')
 THEN RAISE EXCEPTION 'No durable user candidates'; END IF;
 RAISE NOTICE 'PASS repeat stable durable candidates and namespace';
END $$;
UPDATE "DataMigrationSimulationCandidates" SET "NamespaceId"='00000000-0000-0000-0000-000000000099'
WHERE "RunId"=(SELECT "Id" FROM "DataMigrationRuns" WHERE "Job"='MyTeam' AND "DryRun" ORDER BY "StartedOn" DESC LIMIT 1)
 AND "EntityType"='Region';
'@
Run 'Training' 'Failed' 'WrongOrMissingSimulationCandidateNamespace'
CheckProof
Target 'UPDATE "DataMigrationSimulationCandidates" c SET "NamespaceId"=s."NamespaceId" FROM "DataMigrationSimulations" s WHERE s."RunId"=c."RunId";'
Run 'Training'
Target @'
UPDATE "DataMigrationSimulations" SET "MyTeamRunId"='77777777-7777-4777-8777-777777777777'
WHERE "RunId"=(SELECT "Id" FROM "DataMigrationRuns" WHERE "Job"='MyTeam' AND "DryRun" ORDER BY "StartedOn" DESC LIMIT 1);
'@
Run 'Scheduling' 'Failed' 'WrongOrMissingSimulationParentLineage'
CheckProof
Target 'UPDATE "DataMigrationSimulations" s SET "MyTeamRunId"=s."RunId" FROM "DataMigrationRuns" r WHERE r."Id"=s."RunId" AND r."Job"=''MyTeam'';'
Run 'Scheduling'
Source @'
INSERT INTO "Assignment" SELECT (jsonb_populate_record(NULL::"Assignment",to_jsonb(a)||jsonb_build_object(
'Id',910099,'Name','Simulation stale input probe','CreatedOn',now(),'UpdatedOn',now()))).*
FROM "Assignment" a WHERE a."Id"=900020;
'@
Run 'Training' 'Failed' 'StaleSimulationInputsOrTargetBaseline'
CheckProof
Source 'DELETE FROM "Assignment" WHERE "Id"=910099;'
Run 'Training'
Target 'UPDATE "Roles" SET "Name"=''Simulation stale Staff probe'' WHERE "Name"=''Staff'';'
SaveProof
Run 'Scheduling' 'Failed' 'StaleSimulationInputsOrTargetBaseline'
CheckProof
Target 'UPDATE "Roles" SET "Name"=''Staff'' WHERE "Name"=''Simulation stale Staff probe'';'
SaveProof
Run 'Scheduling'
CheckProof
Run 'Report' 'NeedsAction' 'SimulationOnlyNotAppliedLineage'
CheckProof
