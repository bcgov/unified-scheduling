\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
  IF current_database()<>'unified_ss_rehearsal_ready_v4_rollback'
    OR NOT EXISTS (SELECT 1 FROM "DataMigrationRuns" WHERE "Id"='602a64f1-9679-4f79-a668-7b73eda42d22' AND "Status"='Completed') THEN
    RAISE EXCEPTION 'Named mixed-update rollback fixture required';
  END IF;
END $$;
CREATE FUNCTION public.ss_interrupt_rollback_probe() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='40001',MESSAGE='Synthetic inverse interruption'; END $$;
CREATE TRIGGER ss_interrupt_rollback_probe BEFORE DELETE ON public."LoanOutEntries"
  FOR EACH ROW EXECUTE FUNCTION public.ss_interrupt_rollback_probe();
COMMIT;
