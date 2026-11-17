\set ON_ERROR_STOP on
-- Metadata-only performance regression; ALL inserted probes roll back.
-- Forced sequential plan is a labeled counterfactual, not business parity.
BEGIN;
SET LOCAL max_parallel_workers_per_gather=0;
DO $$ BEGIN
 IF current_database()<>'unified_ss_rehearsal_adverse_valid_v20'
 OR EXISTS(SELECT 1 FROM "DataMigrationRecords")
 THEN RAISE EXCEPTION 'Fresh isolated metadata benchmark required'; END IF;
END $$;
INSERT INTO "DataMigrationRecords" ("Source","EntityType","LegacyId","TargetType","TargetKey","PayloadHash","IsDeleted")
SELECT 'SS','ScaleOnly','scale:'||g::text,'AssignmentEntry',(g%10000)::text,repeat('0',64),false
FROM generate_series(1,100000) g;
ANALYZE "DataMigrationRecords";
DO $$
DECLARE indexed jsonb; sequential jsonb;
BEGIN
 EXECUTE $q$EXPLAIN(ANALYZE,BUFFERS,FORMAT JSON)
 SELECT "Id" FROM "DataMigrationRecords" WHERE "Source"='SS'
 AND "TargetType"='AssignmentEntry' AND "TargetKey"='5000'$q$ INTO indexed;
 IF NOT jsonb_path_exists(indexed,'$.**."Index Name" ? (@ == "IX_DataMigrationRecords_Source_TargetType_TargetKey")')
 OR indexed#>>'{0,Plan,Actual Rows}'<>'10'
 THEN RAISE EXCEPTION 'Reverse alias lookup index/cardinality regression'; END IF;
 PERFORM set_config('enable_indexscan','off',true);
 PERFORM set_config('enable_bitmapscan','off',true);
 EXECUTE $q$EXPLAIN(ANALYZE,BUFFERS,FORMAT JSON)
 SELECT "Id" FROM "DataMigrationRecords" WHERE "Source"='SS'
 AND "TargetType"='AssignmentEntry' AND "TargetKey"='5000'$q$ INTO sequential;
 IF sequential#>>'{0,Plan,Node Type}'<>'Seq Scan'
 OR sequential#>>'{0,Plan,Actual Rows}'<>'10'
 THEN RAISE EXCEPTION 'Counterfactual comparison did not execute equivalent row selection'; END IF;
 RAISE NOTICE 'PASS actual100000 metadata roots/10aliases: index-hit-blocks=%, forced-seq-hit-blocks=%, index-ms=%, forced-seq-ms=%',
 indexed#>>'{0,Plan,Shared Hit Blocks}',sequential#>>'{0,Plan,Shared Hit Blocks}',
 indexed#>>'{0,Execution Time}',sequential#>>'{0,Execution Time}';
END $$;
ROLLBACK;
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM "DataMigrationRecords")
 THEN RAISE EXCEPTION 'Benchmark probe escaped rollback'; END IF;
 RAISE NOTICE 'PASS metadata probes entirely rolled back';
END $$;
