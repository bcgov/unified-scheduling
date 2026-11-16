\set ON_ERROR_STOP on
-- Remove private source-value copies from earlier dedicated rehearsal audit logs.
-- Business targets, audit identity/action, run outcomes and failures are NOT removed.
BEGIN;
DO $$
BEGIN
    IF current_database() NOT IN ('unified_ss_rehearsal','unified_ss_rehearsal_ready',
        'unified_ss_rehearsal_ready_v2','unified_ss_rehearsal_ready_v3',
        'unified_ss_rehearsal_ready_v3_publish','unified_ss_rehearsal_ready_v3_drift',
        'unified_ss_rehearsal_ready_v4')
        OR NOT EXISTS (SELECT 1 FROM "DataMigrationRuns" WHERE "Source"='SS') THEN
        RAISE EXCEPTION 'Audit redaction requires a named isolated SS rehearsal.';
    END IF;
END $$;
CREATE TEMP TABLE private_audit_fields (name text PRIMARY KEY) ON COMMIT DROP;
INSERT INTO private_audit_fields VALUES ('firstname'),('lastname'),('email'),('idirname'),
    ('idirid'),('keycloakid'),('badgenumber'),('employeenumber'),('name'),('description'),
    ('title'),('notes'),('note'),('comment'),('expiryreason'),('cancellationreason'),
    ('cancelledreason'),('photo');
UPDATE "AuditRecords" a SET
    "OldValues" = CASE WHEN a."OldValues" IS NULL THEN NULL ELSE
        (SELECT coalesce(jsonb_object_agg(k,v),'{}'::jsonb) FROM jsonb_each(a."OldValues") AS e(k,v)
         WHERE lower(k) NOT IN (SELECT name FROM private_audit_fields)) END,
    "NewValues" = CASE WHEN a."NewValues" IS NULL THEN NULL ELSE
        (SELECT coalesce(jsonb_object_agg(k,v),'{}'::jsonb) FROM jsonb_each(a."NewValues") AS e(k,v)
         WHERE lower(k) NOT IN (SELECT name FROM private_audit_fields)) END,
    "ChangedColumns" = CASE WHEN a."ChangedColumns" IS NULL THEN NULL ELSE
        ARRAY(SELECT col FROM unnest(a."ChangedColumns") col
              WHERE lower(col) NOT IN (SELECT name FROM private_audit_fields)) END
WHERE EXISTS (
    SELECT 1 FROM jsonb_object_keys(coalesce(a."OldValues",'{}'::jsonb) || coalesce(a."NewValues",'{}'::jsonb)) k
    WHERE lower(k) IN (SELECT name FROM private_audit_fields)
);
COMMIT;
