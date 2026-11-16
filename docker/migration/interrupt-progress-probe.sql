\set ON_ERROR_STOP on
DO $$ BEGIN
    IF current_database() <> 'unified_ss_rehearsal_ready_v4_probe'
        OR EXISTS (SELECT 1 FROM "DataMigrationRecords") THEN
        RAISE EXCEPTION 'Interruption probe requires the fresh named probe target.';
    END IF;
END $$;
CREATE FUNCTION ss_probe_interrupt() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW."EntityType"='User' THEN
        RAISE EXCEPTION 'Synthetic checkpoint interruption' USING ERRCODE='40001';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER ss_probe_interrupt BEFORE INSERT ON "DataMigrationWorkItems"
    FOR EACH ROW EXECUTE FUNCTION ss_probe_interrupt();
