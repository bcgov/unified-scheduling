\set ON_ERROR_STOP on
DO $$ BEGIN IF current_database() NOT IN ('unified_ss_rehearsal_repartition_v4','unified_ss_rehearsal_repartition_v5','unified_ss_rehearsal_repartition_history_v5') THEN RAISE EXCEPTION 'Isolated target required'; END IF; END $$;
CREATE TABLE IF NOT EXISTS "SsGraphProbeProof" ("Key" text PRIMARY KEY, "Digest" text NOT NULL);
CREATE OR REPLACE FUNCTION public.ss_probe_scheduling_hash() RETURNS text LANGUAGE sql STABLE AS $$
 SELECT encode(sha256(convert_to(jsonb_build_object(
 'definitions',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AssignmentDefinitions" t),
 'shifts',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "ShiftEntries" t),
 'shiftUsers',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "ShiftEntryUsers" t),
 'duties',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AssignmentEntries" t),
 'links',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "ShiftAssignmentEntries" t),
 'linkUsers',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "ShiftAssignmentEntryUsers" t),
 'series',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AssignmentSeries" t),
 'events',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "Events" t WHERE "SourceModule"='scheduling'),
 'eventSeries',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "EventSeries" t)
 )::text,'UTF8')),'hex');
$$;
INSERT INTO "SsGraphProbeProof" VALUES ('before',public.ss_probe_scheduling_hash())
ON CONFLICT ("Key") DO UPDATE SET "Digest"=excluded."Digest";
