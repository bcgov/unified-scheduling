namespace Unified.Db.DataMigration;

// Installed explicitly by the migration. With no transaction-local migration run
// setting these triggers return immediately and never change ordinary application DML.
public static class SsRollbackSchema
{
    // V1 is immutable because an applied migration references it. A later migration
    // corrects xmin capture: row savepoints use subtransaction IDs, not the top xid.
    public static string FixSubtransactionVersions =>
        Install[
            ..Install.IndexOf(
                "CREATE OR REPLACE FUNCTION public.ss_journal_immutable",
                StringComparison.Ordinal
            )
        ]
            .Replace(
                "photochanged boolean := false;",
                "photochanged boolean := false;\n capturedversion text := 'missing';",
                StringComparison.Ordinal
            )
            .Replace(
                "INSERT INTO public.\"DataMigrationJournals\"",
                """
                IF newj IS NOT NULL THEN
                  EXECUTE format('SELECT t.xmin::text FROM %I.%I t WHERE to_jsonb(t) @> $1',TG_TABLE_SCHEMA,TG_TABLE_NAME)
                    INTO capturedversion USING keys;
                END IF;
                INSERT INTO public."DataMigrationJournals"
                """,
                StringComparison.Ordinal
            )
            .Replace(
                "CASE WHEN newj IS NULL THEN 'missing' ELSE (pg_current_xact_id()::text::numeric % 4294967296)::text END",
                "capturedversion",
                StringComparison.Ordinal
            );

    public const string Install = """
        CREATE EXTENSION IF NOT EXISTS pgcrypto;
        CREATE OR REPLACE FUNCTION public.ss_capture_rollback() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE
            r uuid := nullif(current_setting('unified_ss.run',true),'')::uuid;
            k text;
            kid uuid;
            oldj jsonb;
            newj jsonb;
            keys jsonb := '{}'::jsonb;
            col text;
            changed text[] := ARRAY[]::text[];
            hadphoto boolean := false;
            photochanged boolean := false;
        BEGIN
            IF r IS NULL THEN
                IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
            END IF;
            k := nullif(current_setting('unified_ss.key',true),'');
            kid := nullif(current_setting('unified_ss.key_id',true),'')::uuid;
            IF k IS NULL OR kid IS NULL THEN RAISE EXCEPTION 'SS rollback capture key unavailable'; END IF;
            IF TG_OP <> 'INSERT' THEN oldj := to_jsonb(OLD)-'Photo'; END IF;
            IF TG_OP <> 'DELETE' THEN newj := to_jsonb(NEW)-'Photo'; END IF;
            IF TG_TABLE_NAME='Users' THEN
                IF TG_OP<>'INSERT' THEN hadphoto := OLD."Photo" IS NOT NULL; END IF;
                IF TG_OP='UPDATE' THEN photochanged := OLD."Photo" IS DISTINCT FROM NEW."Photo"; END IF;
            END IF;
            FOR col IN SELECT jsonb_array_elements_text(TG_ARGV[0]::jsonb) LOOP
                keys := keys || jsonb_build_object(col,coalesce(newj,oldj)->col);
            END LOOP;
            IF TG_OP='UPDATE' THEN
                SELECT coalesce(array_agg(x ORDER BY x),ARRAY[]::text[]) INTO changed
                FROM jsonb_object_keys(oldj || newj) x
                WHERE oldj->x IS DISTINCT FROM newj->x
                  AND x NOT IN ('CreatedOn','CreatedBy','UpdatedOn','UpdatedBy');
                IF photochanged THEN changed := array_append(changed,'Photo'); END IF;
            END IF;
            INSERT INTO public."DataMigrationJournals"
              ("RunId","KeyId","SchemaName","TableName","RowKey","Operation","BeforeImage",
               "AfterHash","AfterVersion","ChangedColumns","BeforeHadPhoto","PhotoChanged")
            VALUES(r,kid,TG_TABLE_SCHEMA,TG_TABLE_NAME,keys,substr(TG_OP,1,1),
              CASE WHEN oldj IS NULL THEN NULL ELSE pgp_sym_encrypt(oldj::text,k,'cipher-algo=aes256,compress-algo=1') END,
              CASE WHEN newj IS NULL THEN 'missing' ELSE encode(digest(newj::text,'sha256'),'hex') END,
              CASE WHEN newj IS NULL THEN 'missing' ELSE (pg_current_xact_id()::text::numeric % 4294967296)::text END,
              changed,hadphoto,photochanged);
            IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
        END $$;
        CREATE OR REPLACE FUNCTION public.ss_journal_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'SS rollback journals are immutable'; END $$;
        CREATE TRIGGER ss_journal_immutable BEFORE UPDATE OR DELETE ON public."DataMigrationJournals"
          FOR EACH ROW EXECUTE FUNCTION public.ss_journal_immutable();
        DO $$
        DECLARE t record;
        BEGIN
          FOR t IN
            SELECT n.nspname,c.relname,jsonb_agg(a.attname ORDER BY p.ord)::text AS keys
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            JOIN pg_index i ON i.indrelid=c.oid AND i.indisprimary
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY p(attnum,ord)
            JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=p.attnum
            WHERE n.nspname='public' AND c.relkind='r'
              AND c.relname NOT LIKE '__EF%'
              AND c.relname NOT ILIKE '%audit%'
              AND c.relname <> 'DataProtectionKeys'
              AND (c.relname NOT LIKE 'DataMigration%' OR c.relname IN
                ('DataMigrationRecords','DataMigrationMutations','DataMigrationWorkItems','DataMigrationCheckpoints'))
            GROUP BY n.nspname,c.relname
          LOOP
            EXECUTE format('CREATE TRIGGER ss_rollback_capture AFTER INSERT OR UPDATE OR DELETE ON %I.%I FOR EACH ROW EXECUTE FUNCTION public.ss_capture_rollback(%L)',t.nspname,t.relname,t.keys);
          END LOOP;
        END $$;
        """;
}
