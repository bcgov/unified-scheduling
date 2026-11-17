\set ON_ERROR_STOP on
-- Synthetic clone ONLY. Never apply this fixture tuning to the original source.
DO $$
DECLARE t record;
BEGIN
 IF current_database()<>'ss_adverse_valid_v18'
 OR NOT EXISTS(SELECT 1 FROM pg_database WHERE datname=current_database()
 AND shobj_description(oid,'pg_database')='SS SYNTHETIC ADVERSE PAGING V18')
 THEN RAISE EXCEPTION 'Exact isolated synthetic clone required'; END IF;
 FOR t IN
 SELECT c.table_name FROM information_schema.columns c
 WHERE c.table_schema='public' AND c.column_name IN ('Id','CreatedOn','UpdatedOn')
 GROUP BY c.table_name HAVING count(DISTINCT c.column_name)=3
 LOOP
  EXECUTE format('CREATE INDEX %I ON public.%I ((COALESCE("UpdatedOn","CreatedOn")),"Id")',
    'ss_migration_page_'||lower(t.table_name),t.table_name);
 END LOOP;
END $$;
