-- Synthetic-only change: never execute on the ready-v4 oracle fixture or production.
BEGIN;
DO $$
DECLARE affected integer;
BEGIN
  IF current_database() <> 'ss_ready_v4_probe'
     OR (SELECT shobj_description(oid,'pg_database') FROM pg_database
         WHERE datname=current_database()) IS DISTINCT FROM 'SS SYNTHETIC'
  THEN RAISE EXCEPTION 'Exact marked synthetic probe database required'; END IF;
  UPDATE "Duty" SET "Comment"='Synthetic duty comment-only replay probe v2',
    "UpdatedOn"=clock_timestamp() WHERE "Id"=900000;
  GET DIAGNOSTICS affected = ROW_COUNT;
  IF affected<>1 THEN RAISE EXCEPTION 'Expected exactly one synthetic probe duty'; END IF;
END $$;
COMMIT;
