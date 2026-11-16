-- SS synthetic data generator (writes to a disposable database only).
--
-- Purpose: build a synthetic Sheriff Scheduling (SS) database with the shape of production, for
-- building and testing the SS -> Unified migration. No production rows are read or copied.
-- Every person, comment and identifier is invented. Volumes and patterns come from the
-- aggregate extracts (EX/AG/AD/SP/OD/SH); edit the parameters below to match ss-shape-extracts.md.
--
-- The script also plants the edge cases the mapping rules handle (G-3, G-8, G-12e, G-13c, G-16,
-- G-17, G-19, Q-L4, Q-L6, Type 7, regions, SP-04). Each planted row is listed in
-- synthetic.case_manifest with the outcome the migration's final report must give. The class
-- codes are proposed names; align them with the report catalogue when it is built.
--
-- Target: an EMPTY PostgreSQL 12 database built from the SS EF migrations (see "Synthetic SS
-- Database" in migration-job-design.md). Refuses to run when SS schedule tables already hold rows.
-- SS tables are written in schema public exactly as SS defines them. Helper tables, the case
-- manifest and the parameters go to schema synthetic.
--
-- Run (psql 12+):
--   psql -X -v ON_ERROR_STOP=1 -f ss-synthetic-generator.sql [-v name=value ...]
-- Parameters (all optional):
--   seed            text      ss-synthetic-v1   same seed -> same database
--   sheriffs        int       80
--   history_start   date      2025-01-01        first local date of generated history
--   cutover         date      2026-12-01        approved CutoverDate; later dates are "future"
--   future_weeks    int       26                future duties beyond the cutover (SS Populate)
--   seed_file       path      (none)            SheriffStandardTraining seed split from the SS
--                                               migration script; loaded after its courses exist
--   catalogue_path  path      (none)            server-side CSV "type,code" replacing the built-in
--                                               Types 1-4 subset, for example built from EX-10
--
-- Assumptions: all synthetic locations use America/Vancouver. Future shifts exist only at
-- Synthetic Court 01 (production: Surrey only, SP-05). Stored Shift.OvertimeHours follows the SS
-- rule at write time (sum of the sheriff's shifts in the local day - 8 h, on the last shift), so
-- duplicate shifts inflate it as in production (OD-01).

\set ON_ERROR_STOP on
\set QUIET on
\pset pager off

\if :{?seed}
\else
\set seed 'ss-synthetic-v1'
\endif
\if :{?sheriffs}
\else
\set sheriffs 80
\endif
\if :{?history_start}
\else
\set history_start '2025-01-01'
\endif
\if :{?cutover}
\else
\set cutover '2026-12-01'
\endif
\if :{?future_weeks}
\else
\set future_weeks 26
\endif
\set sys '''00000000-0000-0000-0000-000000000001''::uuid'

BEGIN;
SET LOCAL TimeZone = 'UTC';
SELECT set_config('synthetic.seed', :'seed', true) \g /dev/null

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "Shift") OR EXISTS (SELECT 1 FROM "Duty") OR EXISTS (SELECT 1 FROM "Assignment")
       OR EXISTS (SELECT 1 FROM "SheriffLeave") OR EXISTS (SELECT 1 FROM "SheriffTraining") THEN
        RAISE EXCEPTION 'Refused: SS schedule tables already hold rows. Use an empty database built from the SS EF migrations.';
    END IF;
    IF to_regnamespace('synthetic') IS NOT NULL THEN
        RAISE EXCEPTION 'Refused: schema synthetic already exists.';
    END IF;
    IF (SELECT max("MigrationId") FROM "__EFMigrationsHistory") IS DISTINCT FROM '20250723232143_SeedDataForSheriffStandardTraining' THEN
        RAISE WARNING 'Latest SS migration differs from the one this generator was written for (20250723232143).';
    END IF;
END $$;

CREATE SCHEMA synthetic;
CREATE TABLE synthetic.profile (k text PRIMARY KEY, v text);
INSERT INTO synthetic.profile VALUES
    ('seed', :'seed'), ('sheriffs', :'sheriffs'), ('history_start', :'history_start'),
    ('cutover', :'cutover'), ('future_weeks', :'future_weeks'), ('generated_at_utc', now()::text),
    ('generator', 'ss-synthetic-generator.sql v1');
CREATE TABLE synthetic.case_manifest (
    case_id text NOT NULL, rule_ref text NOT NULL, source_table text NOT NULL, source_id text NOT NULL,
    expected_outcome text NOT NULL, class_code text NOT NULL, note text);

CREATE FUNCTION synthetic.rnd(k text) RETURNS double precision LANGUAGE sql STABLE AS
$$ SELECT (('x' || substr(md5(current_setting('synthetic.seed') || '|' || k), 1, 8))::bit(32)::bigint / 4294967296.0)::double precision $$;
CREATE FUNCTION synthetic.uid(k text) RETURNS uuid LANGUAGE sql STABLE AS
$$ SELECT md5('uid|' || current_setting('synthetic.seed') || '|' || k)::uuid $$;
CREATE FUNCTION synthetic.at(d date, t time) RETURNS timestamptz LANGUAGE sql IMMUTABLE AS
$$ SELECT (d + t) AT TIME ZONE 'America/Vancouver' $$;
CREATE FUNCTION synthetic.sync_sequences() RETURNS void LANGUAGE plpgsql AS $$
DECLARE r record; m bigint;
BEGIN
    FOR r IN
        SELECT c.relname AS tbl, a.attname AS col, pg_get_serial_sequence(format('%I', c.relname), a.attname) AS seq
        FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace JOIN pg_attribute a ON a.attrelid = c.oid
        WHERE n.nspname = 'public' AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
          AND pg_get_serial_sequence(format('%I', c.relname), a.attname) IS NOT NULL
    LOOP
        EXECUTE format('SELECT coalesce(max(%I), 0) FROM %I', r.col, r.tbl) INTO m;
        PERFORM setval(r.seq, greatest(m, 1), m > 0);
    END LOOP;
END $$;

-- Migration seeds use explicit Ids and leave the sequences at 1.
SELECT synthetic.sync_sequences() \g /dev/null

-- ---------------------------------------------------------------------------------------------
-- Regions and locations
INSERT INTO "Region" ("Code", "Name", "JustinId", "CreatedOn") VALUES
    ('SYN-N', 'Synthetic North Region', 9001, now()),
    ('SYN-S', 'Synthetic South Region', 9002, now()),
    (NULL, 'Synthetic Operational Region', NULL, now()),
    (NULL, 'Synthetic Operational Region', NULL, now());
INSERT INTO synthetic.case_manifest
SELECT 'C-REG-01', 'Null-JustinId region rule', 'Region', "Id"::text, 'Rejected', 'REGION_AMBIGUOUS_NAME',
       'Two null-JustinId regions share one name'
FROM "Region" WHERE "Name" = 'Synthetic Operational Region';

CREATE TABLE synthetic.loc AS
SELECT i, nextval(pg_get_serial_sequence('"Location"', 'Id'))::int AS id FROM generate_series(1, 6) i ORDER BY i;
INSERT INTO "Location" ("Id", "Name", "JustinCode", "AgencyId", "RegionId", "Timezone", "CreatedOn")
SELECT l.id, format('Synthetic Court %s', lpad(l.i::text, 2, '0')), format('S%s', lpad(l.i::text, 2, '0')),
       format('99%s', lpad(l.i::text, 2, '0')),
       (SELECT "Id" FROM "Region" WHERE "Code" = CASE WHEN l.i <= 3 THEN 'SYN-N' ELSE 'SYN-S' END),
       'America/Vancouver', now()
FROM synthetic.loc l ORDER BY l.i;

-- ---------------------------------------------------------------------------------------------
-- Lookup codes. Types 1-4 codes are real production codes (public reference data, EX-10) so the
-- approved (Type, Code) crosswalk resolves them. Type 0 court rooms are per location.
CREATE TABLE synthetic.catalogue (type int NOT NULL, code text NOT NULL);
\if :{?catalogue_path}
COPY synthetic.catalogue (type, code) FROM :'catalogue_path' WITH (FORMAT csv, HEADER true);
\else
INSERT INTO synthetic.catalogue VALUES
    (1, 'Court Sergeant'), (1, 'Inspector'), (1, 'Administration'), (1, 'ADMINISTRATION'),
    (1, 'Staff Sergeant'), (1, 'Test'), (2, 'Cells'), (2, 'Control'), (2, 'Jail Sergeant'),
    (3, 'Escort'), (4, 'Administration'), (4, 'Documents'), (4, 'Leave Waitlist'), (4, 'Supervisor');
\endif

CREATE TABLE synthetic.code AS
SELECT c.type, c.code, l.i AS li, l.id AS loc_id, nextval(pg_get_serial_sequence('"LookupCode"', 'Id'))::int AS id
FROM (SELECT type, code FROM synthetic.catalogue
      UNION ALL SELECT 0, 'Courtroom ' || k FROM generate_series(1, 3) k) c
CROSS JOIN synthetic.loc l
ORDER BY l.i, c.type, c.code;
UPDATE synthetic.code SET code = 'Courtroom ' || li || '0' || right(code, 1) WHERE type = 0;
INSERT INTO "LookupCode" ("Id", "Type", "Code", "Description", "LocationId", "CreatedOn")
SELECT id, type, code, code, loc_id, now() FROM synthetic.code ORDER BY id;
INSERT INTO "LookupSortOrder" ("LookupCodeId", "LocationId", "SortOrder", "CreatedOn")
SELECT id, loc_id, row_number() OVER (PARTITION BY loc_id ORDER BY type, code), now() FROM synthetic.code;

-- Type 5 leave. Seeds hold STIP, Annual, Illness, Special.
INSERT INTO "LookupCode" ("Type", "Code", "Description", "LocationId", "CreatedOn") VALUES
    (5, 'CTO', 'CTO', NULL, now()),
    (5, 'A/L', 'A/L', NULL, now()),
    (5, 'Bereavement', 'Bereavement', NULL, now()),
    (5, 'Synthetic Unknown Leave', 'Synthetic Unknown Leave', NULL, now());
INSERT INTO "LookupCode" ("Type", "Code", "Description", "LocationId", "CreatedOn")
SELECT 5, 'Synthetic Local Leave', 'Synthetic Local Leave', id, now() FROM synthetic.loc WHERE i = 1;
INSERT INTO synthetic.case_manifest
SELECT CASE WHEN "LocationId" IS NULL THEN 'C-QL6-01' ELSE 'C-QL4-01' END,
       CASE WHEN "LocationId" IS NULL THEN 'Q-L6' ELSE 'Q-L4' END, 'LookupCode', "Id"::text, 'Rejected',
       CASE WHEN "LocationId" IS NULL THEN 'QL6_UNKNOWN_CODE' ELSE 'QL4_SCOPE' END,
       CASE WHEN "LocationId" IS NULL THEN 'Leave code outside the approved snapshot list' ELSE 'Leave code with LocationId set' END
FROM "LookupCode" WHERE "Type" = 5 AND "Code" IN ('Synthetic Unknown Leave', 'Synthetic Local Leave');

-- Type 6 courses. These Ids are fixed by the SS SheriffStandardTraining seed migration.
INSERT INTO "LookupCode" ("Id", "Type", "Code", "Description", "Mandatory", "ValidityPeriod", "AdvanceNotice",
                          "Category", "Rotating", "EffectiveDate", "CreatedOn")
SELECT t.id, 6, format('Synthetic Standard Course %s', lpad(t.n::text, 2, '0')),
       CASE WHEN t.n % 4 = 0 THEN NULL ELSE format('Synthetic standard course %s', t.n) END,
       true, (ARRAY[365, 730, 1095, 1461, 1825, 2000, 0])[1 + t.n % 7], (ARRAY[0, 30, 60, 90])[1 + t.n % 4],
       (ARRAY['Provincial Standard', 'Branch', 'Local', NULL])[1 + t.n % 4], t.n % 5 = 0,
       CASE WHEN t.n % 3 = 0 THEN NULL ELSE synthetic.at(:'history_start'::date - 365, '00:00') END, now()
FROM unnest(ARRAY[1716948, 1716951, 1716954, 1716955, 1716956, 1716958, 1716959, 1716960, 1716961,
                  1718646, 1718650, 1718748]) WITH ORDINALITY AS t(id, n);
INSERT INTO synthetic.case_manifest
SELECT 'C-QT6C-01', 'Q-T6C', 'LookupCode', "Id"::text, 'Migrated', 'QT6C_CATEGORY_LINK',
       'New course with category "' || "Category" || '"'
FROM "LookupCode" WHERE "Type" = 6 AND "Category" IS NOT NULL;

-- Type 7: a Description-only match case. Seeds hold the seven standard ranks.
INSERT INTO "LookupCode" ("Type", "Code", "Description", "CreatedOn") VALUES (7, 'CSH', 'Constable Sheriff', now());

-- ---------------------------------------------------------------------------------------------
-- People
CREATE TABLE synthetic.sheriff AS
SELECT i, synthetic.uid('sheriff|' || i) AS id, 1 + (i - 1) % 6 AS home_i,
       CASE WHEN r < 0.70 THEN 'Deputy Sheriff' WHEN r < 0.85 THEN 'Sergeant' WHEN r < 0.93 THEN 'Staff Sergeant'
            WHEN r < 0.98 THEN 'Inspector' ELSE 'Superintendent' END AS rank
FROM (SELECT i, synthetic.rnd('rank|' || i) AS r FROM generate_series(1, :sheriffs) i) x ORDER BY i;
UPDATE synthetic.sheriff SET rank = 'Constable Sheriff' WHERE i = 2;
UPDATE synthetic.sheriff SET rank = 'Synthetic Unknown Rank' WHERE i = 3;

INSERT INTO "User" ("Id", "IdirId", "HomeLocationId", "FirstName", "LastName", "Email", "Discriminator", "Gender",
                    "BadgeNumber", "Rank", "IdirName", "IsEnabled", "KeyCloakId", "Excused", "LastLogin", "CreatedOn", "CreatedById")
SELECT s.id, synthetic.uid('idir|' || s.i), l.id, 'Synthetic', format('Sheriff %s', lpad(s.i::text, 3, '0')),
       format('sheriff%s@synthetic.invalid', lpad(s.i::text, 3, '0')), 'Sheriff', s.i % 3,
       format('S%s', lpad(s.i::text, 4, '0')), s.rank, format('SYN%s', lpad(s.i::text, 3, '0')),
       s.i % 20 <> 0, synthetic.uid('kc|' || s.i), s.i % 33 = 0, now() - s.i * interval '1 day',
       synthetic.at(:'history_start'::date - 400, '09:00'), :sys
FROM synthetic.sheriff s JOIN synthetic.loc l ON l.i = s.home_i ORDER BY s.i;
INSERT INTO "User" ("Id", "FirstName", "LastName", "Email", "Discriminator", "IdirName", "IsEnabled", "KeyCloakId", "CreatedOn")
SELECT synthetic.uid('admin|' || i), 'Synthetic', format('Administrator %s', i), format('admin%s@synthetic.invalid', i),
       'User', format('SYNADM%s', i), true, synthetic.uid('kcadm|' || i), now()
FROM generate_series(1, 2) i;
INSERT INTO synthetic.case_manifest
SELECT CASE WHEN i = 2 THEN 'C-T7-01' ELSE 'C-T7-02' END, 'Type 7', 'User', id::text,
       CASE WHEN i = 2 THEN 'Migrated' ELSE 'Rejected' END,
       CASE WHEN i = 2 THEN 'T7_DESCRIPTION_MATCH' ELSE 'T7_NO_MATCH' END,
       CASE WHEN i = 2 THEN 'Rank text matches a Type 7 Description, not a Code' ELSE 'Rank text matches no Type 7 Code or Description' END
FROM synthetic.sheriff WHERE i IN (2, 3);

INSERT INTO "UserRole" ("UserId", "RoleId", "EffectiveDate", "CreatedOn")
SELECT id, 3, synthetic.at(:'history_start'::date - 400, '00:00'), now() FROM synthetic.sheriff
UNION ALL
SELECT id, 2, synthetic.at(:'history_start'::date - 400, '00:00'), now() FROM synthetic.sheriff WHERE rank NOT IN ('Deputy Sheriff', 'Constable Sheriff')
UNION ALL
SELECT synthetic.uid('admin|' || i), 1, synthetic.at(:'history_start'::date - 400, '00:00'), now() FROM generate_series(1, 2) i;

-- ---------------------------------------------------------------------------------------------
-- Calendar and per-sheriff day plan
CREATE TABLE synthetic.day AS
SELECT g::date AS d, extract(isodow FROM g)::int AS dow, date_trunc('week', g)::date AS wk, g::date <= :'cutover'::date AS past
FROM generate_series(:'history_start'::date, :'cutover'::date + 7 * :future_weeks, interval '1 day') g;

CREATE TABLE synthetic.plan AS
WITH b AS (
    SELECT s.i AS si, s.id AS sid, s.home_i, d.d, d.dow, d.wk, d.past,
           synthetic.rnd('lw|' || s.i || '|' || d.wk) < 0.17 AS leave_wk,
           1 + floor(synthetic.rnd('ll|' || s.i || '|' || d.wk) * 5)::int AS leave_len,
           synthetic.rnd('aw|' || s.i || '|' || d.wk) < 0.02 AS away_wk,
           synthetic.rnd('td|' || s.i || '|' || d.d) < 0.012 AS train_d
    FROM synthetic.sheriff s CROSS JOIN synthetic.day d
)
SELECT si, sid, home_i, d, dow, wk, past,
       CASE WHEN dow > 5 THEN 'off' WHEN leave_wk AND dow <= leave_len THEN 'leave'
            WHEN train_d THEN 'training' ELSE 'work' END AS kind,
       CASE WHEN away_wk AND NOT leave_wk THEN 1 + home_i % 6 ELSE home_i END AS work_i,
       away_wk AND NOT leave_wk AS away
FROM b;

-- ---------------------------------------------------------------------------------------------
-- Shifts: 08:00-16:00 local. Long days add a touching 16:00-18:00 shift (SS SplitLongShifts).
INSERT INTO "Shift" ("StartDate", "EndDate", "LocationId", "SheriffId", "Timezone", "OvertimeHours", "CreatedOn", "CreatedById")
SELECT synthetic.at(p.d, '08:00'), synthetic.at(p.d, '16:00'), l.id, p.sid, 'America/Vancouver', 0,
       synthetic.at(p.d, '08:00') - interval '14 days', :sys
FROM synthetic.plan p JOIN synthetic.loc l ON l.i = p.work_i
WHERE p.kind = 'work' AND (p.past OR p.work_i = 1)
ORDER BY p.d, p.si;
INSERT INTO "Shift" ("StartDate", "EndDate", "LocationId", "SheriffId", "Timezone", "OvertimeHours", "CreatedOn", "CreatedById")
SELECT synthetic.at(p.d, '16:00'), synthetic.at(p.d, '18:00'), l.id, p.sid, 'America/Vancouver', 0,
       synthetic.at(p.d, '08:00') - interval '1 day', :sys
FROM synthetic.plan p JOIN synthetic.loc l ON l.i = p.work_i
WHERE p.kind = 'work' AND (p.past OR p.work_i = 1) AND synthetic.rnd('ld|' || p.si || '|' || p.d) < 0.04
ORDER BY p.d, p.si;

CREATE VIEW synthetic.shift_day AS
SELECT s."Id", s."SheriffId" AS sid, s."LocationId" AS loc, (s."StartDate" AT TIME ZONE 'America/Vancouver')::date AS d,
       count(*) OVER (PARTITION BY s."SheriffId", (s."StartDate" AT TIME ZONE 'America/Vancouver')::date) AS n
FROM "Shift" s WHERE s."ExpiryDate" IS NULL;

-- ---------------------------------------------------------------------------------------------
-- Assignments, duties and slots
CREATE TABLE synthetic.asg AS
SELECT nextval(pg_get_serial_sequence('"Assignment"', 'Id'))::int AS id, c.li, c.loc_id, c.id AS code_id, c.type, c.code,
       CASE c.type WHEN 2 THEN time '08:00' WHEN 3 THEN time '09:00' WHEN 4 THEN time '09:00' ELSE time '08:30' END AS st,
       CASE c.type WHEN 3 THEN time '15:00' WHEN 4 THEN time '15:00' ELSE time '16:00' END AS en,
       CASE WHEN r.r1 < 0.85 THEN 'MTWTF--' WHEN r.r1 < 0.92 THEN 'MTW----' WHEN r.r1 < 0.97 THEN '---TF--' ELSE 'MTWTFSS' END AS mask,
       CASE WHEN r.r2 < 0.12 THEN :'history_start'::date + (60 + floor(r.r3 * 400))::int END AS expiry_d,
       CASE WHEN r.r4 < 0.06 THEN :'history_start'::date + floor(r.r5 * 600)::int END AS adhoc_s,
       28 + floor(r.r3 * 56)::int AS adhoc_len,
       CASE WHEN r.r5 < 0.4 THEN c.code || ' duty' END AS name
FROM synthetic.code c,
     LATERAL (SELECT synthetic.rnd('a1|' || c.id) AS r1, synthetic.rnd('a2|' || c.id) AS r2, synthetic.rnd('a3|' || c.id) AS r3,
                     synthetic.rnd('a4|' || c.id) AS r4, synthetic.rnd('a5|' || c.id) AS r5) r
WHERE c.type = 0 OR (c.code IN ('Test', 'Leave Waitlist') AND c.li = 1)
   OR (c.code NOT IN ('Test', 'Leave Waitlist') AND synthetic.rnd('use|' || c.id) < 0.6)
ORDER BY c.id;
ALTER TABLE synthetic.asg ADD COLUMN case_id text;
-- G-3 excluded codes: one location, a 90-day ad hoc window ending 30 days after the cutover.
UPDATE synthetic.asg SET case_id = 'C-G3-01', expiry_d = NULL, adhoc_s = :'cutover'::date - 60, adhoc_len = 90
WHERE code IN ('Test', 'Leave Waitlist');

-- G-8: same name after trim/upper in one location and type -> merged; same name across types -> split.
INSERT INTO synthetic.asg (id, li, loc_id, code_id, type, code, st, en, mask, expiry_d, adhoc_s, adhoc_len, name, case_id)
SELECT nextval(pg_get_serial_sequence('"Assignment"', 'Id'))::int, c.li, c.loc_id, c.id, c.type, c.code, time '08:30', time '16:00',
       'MTWTF--', NULL, NULL, 0, v.name, v.case_id
FROM (VALUES (2, 1, 'Court Sergeant', 'Front Desk', 'C-G8-01'), (2, 1, 'Court Sergeant', ' front desk', 'C-G8-01'),
             (3, 1, 'Court Sergeant', 'Floater', 'C-G8-02'), (3, 2, 'Cells', 'Floater', 'C-G8-02')) v(li, type, code, name, case_id)
JOIN synthetic.code c ON c.li = v.li AND c.type = v.type AND c.code = v.code;

INSERT INTO "Assignment" ("Id", "LookupCodeId", "AdhocStartDate", "AdhocEndDate", "Timezone", "Name",
                          "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
                          "LocationId", "ExpiryDate", "ExpiryReason", "CreatedOn", "CreatedById", "Start", "End")
SELECT a.id, a.code_id, synthetic.at(a.adhoc_s, '00:00'), synthetic.at(a.adhoc_s + a.adhoc_len, '00:00'),
       'America/Vancouver', a.name,
       substr(a.mask, 1, 1) <> '-', substr(a.mask, 2, 1) <> '-', substr(a.mask, 3, 1) <> '-', substr(a.mask, 4, 1) <> '-',
       substr(a.mask, 5, 1) <> '-', substr(a.mask, 6, 1) <> '-', substr(a.mask, 7, 1) <> '-',
       a.loc_id, synthetic.at(a.expiry_d, '00:00'), CASE WHEN a.expiry_d IS NOT NULL THEN 'Synthetic retired' END,
       synthetic.at(:'history_start'::date - 30, '09:00'), :sys, a.st - time '00:00', a.en - time '00:00'
FROM synthetic.asg a ORDER BY a.id;
INSERT INTO synthetic.case_manifest
SELECT a.case_id, CASE WHEN a.case_id = 'C-G3-01' THEN 'G-3' ELSE 'G-8' END, 'Assignment', a.id::text,
       CASE a.case_id WHEN 'C-G3-01' THEN 'Excluded' WHEN 'C-G8-01' THEN 'Merged' ELSE 'Migrated' END,
       CASE a.case_id WHEN 'C-G3-01' THEN 'G3_EXCLUDED_CODE' WHEN 'C-G8-01' THEN 'G8_MERGE' ELSE 'G8_TYPE_SUFFIX' END,
       CASE a.case_id WHEN 'C-G3-01' THEN 'Assignment under excluded code ' || a.code
                      WHEN 'C-G8-01' THEN 'Names equal after trim/upper in one location and type'
                      ELSE 'Same name in two lookup types; split with type suffix' END
FROM synthetic.asg a WHERE a.case_id IS NOT NULL;

INSERT INTO "Duty" ("StartDate", "EndDate", "LocationId", "AssignmentId", "Timezone", "CreatedOn", "CreatedById")
SELECT synthetic.at(d.d, a.st), synthetic.at(d.d, a.en), a.loc_id, a.id, 'America/Vancouver',
       synthetic.at(d.d, '00:00') - interval '30 days', :sys
FROM synthetic.asg a JOIN synthetic.day d ON substr(a.mask, d.dow, 1) <> '-'
WHERE (a.adhoc_s IS NULL OR d.d BETWEEN a.adhoc_s AND a.adhoc_s + a.adhoc_len - 1)
  AND (a.expiry_d IS NULL OR d.d < a.expiry_d)
ORDER BY d.d, a.id;
-- Extra-staffing (FTE) second duty on the same assignment and date.
INSERT INTO "Duty" ("StartDate", "EndDate", "LocationId", "AssignmentId", "Timezone", "CreatedOn", "CreatedById")
SELECT du."StartDate", du."EndDate", du."LocationId", du."AssignmentId", du."Timezone", du."CreatedOn", :sys
FROM "Duty" du WHERE synthetic.rnd('fte|' || du."Id") < 0.01 ORDER BY du."Id";
INSERT INTO synthetic.case_manifest
SELECT 'C-G3-01', 'G-3', 'Duty', du."Id"::text, 'Excluded', 'G3_EXCLUDED_CODE', 'Duty under excluded code ' || a.code
FROM "Duty" du JOIN synthetic.asg a ON a.id = du."AssignmentId" WHERE a.case_id = 'C-G3-01';

CREATE TABLE synthetic.duty_pick AS
SELECT du."Id" AS duty_id, du."LocationId" AS loc_id, l.i AS li, (du."StartDate" AT TIME ZONE 'America/Vancouver')::date AS d,
       du."StartDate" AS st, du."EndDate" AS en,
       CASE WHEN x.k < 0.02 THEN 'closed' WHEN x.k < 0.035 THEN 'notrequired' WHEN x.k < 0.05 THEN 'notavailable' ELSE 'staff' END AS kind,
       row_number() OVER (PARTITION BY du."LocationId", (du."StartDate" AT TIME ZONE 'America/Vancouver')::date, x.k >= 0.05
                          ORDER BY du."Id") AS rn
FROM "Duty" du JOIN synthetic.loc l ON l.id = du."LocationId", LATERAL (SELECT synthetic.rnd('sf|' || du."Id") AS k) x;
CREATE TABLE synthetic.worker AS
SELECT p.sid, p.si, p.work_i AS li, p.d,
       row_number() OVER (PARTITION BY p.work_i, p.d ORDER BY synthetic.rnd('wk|' || p.si || '|' || p.d)) AS rn
FROM synthetic.plan p WHERE p.kind = 'work' AND (p.past OR p.work_i = 1);

INSERT INTO "DutySlot" ("StartDate", "EndDate", "DutyId", "SheriffId", "Timezone", "LocationId",
                        "IsNotAvailable", "IsNotRequired", "IsClosed", "IsOvertime", "CreatedOn", "CreatedById")
SELECT dp.st, dp.en, dp.duty_id, w.sid, 'America/Vancouver', dp.loc_id,
       dp.kind = 'notavailable', dp.kind = 'notrequired', dp.kind = 'closed', false, dp.st - interval '7 days', :sys
FROM synthetic.duty_pick dp
LEFT JOIN synthetic.worker w ON dp.kind = 'staff' AND w.li = dp.li AND w.d = dp.d AND w.rn = dp.rn
WHERE dp.kind <> 'staff' OR w.sid IS NOT NULL
ORDER BY dp.duty_id;

-- ---------------------------------------------------------------------------------------------
-- Leave, training, loan-outs and acting ranks
INSERT INTO "SheriffLeave" ("LeaveTypeId", "StartDate", "EndDate", "SheriffId", "Timezone", "CreatedOn", "CreatedById")
SELECT (SELECT lc."Id" FROM "LookupCode" lc WHERE lc."Type" = 5 AND lc."LocationId" IS NULL AND lc."Code" =
            CASE WHEN g.r < 0.50 THEN 'Annual' WHEN g.r < 0.65 THEN 'STIP' WHEN g.r < 0.80 THEN 'Illness'
                 WHEN g.r < 0.88 THEN 'Special' WHEN g.r < 0.95 THEN 'CTO' WHEN g.r < 0.98 THEN 'A/L' ELSE 'Bereavement' END),
       synthetic.at(g.d0, '00:00'), synthetic.at(g.d1, '23:59'), g.sid, 'America/Vancouver',
       synthetic.at(g.d0, '00:00') - interval '30 days', :sys
FROM (SELECT sid, si, wk, min(d) AS d0, max(d) AS d1, synthetic.rnd('lt|' || si || '|' || wk) AS r
      FROM synthetic.plan WHERE kind = 'leave' GROUP BY sid, si, wk) g
ORDER BY g.d0, g.si;

INSERT INTO "SheriffTraining" ("TrainingTypeId", "StartDate", "EndDate", "SheriffId", "Timezone",
                               "TrainingCertificationExpiry", "FirstNotice", "CreatedOn", "CreatedById")
SELECT lc."Id", synthetic.at(p.d, '08:00'), synthetic.at(p.d, '16:00'), p.sid, 'America/Vancouver',
       CASE WHEN lc."ValidityPeriod" > 0 THEN synthetic.at(p.d, '16:00') + lc."ValidityPeriod" * interval '1 day' END,
       false, synthetic.at(p.d, '08:00') - interval '30 days', :sys
FROM synthetic.plan p
CROSS JOIN (SELECT array_agg("Id" ORDER BY "Id") AS ids FROM "LookupCode" WHERE "Type" = 6) c
CROSS JOIN LATERAL (SELECT c.ids[1 + floor(synthetic.rnd('tc|' || p.si || '|' || p.d) * array_length(c.ids, 1))::int] AS tid) x
JOIN "LookupCode" lc ON lc."Id" = x.tid
WHERE p.kind = 'training'
ORDER BY p.d, p.si;

INSERT INTO "SheriffAwayLocation" ("LocationId", "StartDate", "EndDate", "SheriffId", "Timezone", "CreatedOn", "CreatedById")
SELECT l.id, synthetic.at(g.d0, '00:00'), synthetic.at(g.d1, '23:59'), g.sid, 'America/Vancouver',
       synthetic.at(g.d0, '00:00') - interval '14 days', :sys
FROM (SELECT sid, si, wk, work_i, min(d) AS d0, max(d) AS d1 FROM synthetic.plan
      WHERE away AND dow <= 5 GROUP BY sid, si, wk, work_i) g
JOIN synthetic.loc l ON l.i = g.work_i
ORDER BY g.d0, g.si;

INSERT INTO "SheriffActingRank" ("Rank", "StartDate", "EndDate", "SheriffId", "Timezone", "CreatedOn", "CreatedById")
SELECT 'Sergeant', synthetic.at(x.d0, '00:00'), synthetic.at(x.d0 + x.len, '23:59'), x.id, 'America/Vancouver',
       synthetic.at(x.d0, '00:00') - interval '7 days', :sys
FROM (SELECT s.i, s.id, :'history_start'::date + floor(synthetic.rnd('ard|' || s.i) * 500)::int AS d0,
             30 + floor(synthetic.rnd('arl|' || s.i) * 60)::int AS len
      FROM synthetic.sheriff s WHERE s.rank = 'Deputy Sheriff' AND synthetic.rnd('ar|' || s.i) < 0.12) x
ORDER BY x.i;

-- ---------------------------------------------------------------------------------------------
-- Planted edge cases. Each pick uses its own hash key and avoids rows already used by a case.

-- G-13c class 4: a staffed past slot whose sheriff has no live shift (shift deleted in SS).
WITH pick AS (
    SELECT DISTINCT ON (sd."Id") sd."Id" AS shift_id, ds."Id" AS slot_id
    FROM "DutySlot" ds JOIN synthetic.shift_day sd
      ON sd.sid = ds."SheriffId" AND sd.d = (ds."StartDate" AT TIME ZONE 'America/Vancouver')::date AND sd.n = 1
    WHERE ds."StartDate" < synthetic.at(:'cutover'::date, '00:00')
    ORDER BY sd."Id", ds."Id"
), lim AS (SELECT * FROM pick ORDER BY synthetic.rnd('g13c|' || slot_id) LIMIT 5),
up AS (UPDATE "Shift" s SET "ExpiryDate" = s."StartDate"
       FROM lim WHERE s."Id" = lim.shift_id RETURNING lim.slot_id)
INSERT INTO synthetic.case_manifest
SELECT 'C-G13C-01', 'G-13c', 'DutySlot', ds."Id"::text, 'DroppedBadData', 'G13C_NO_SHIFT', 'Staffed slot with no live shift that day'
FROM up JOIN "DutySlot" ds ON ds."DutyId" = (SELECT "DutyId" FROM "DutySlot" WHERE "Id" = up.slot_id) AND ds."SheriffId" IS NOT NULL;

-- G-17 class A: identical duplicate shift on a past one-shift day. SS stored overtime doubles (G-19).
WITH pick AS (
    SELECT sd."Id" FROM synthetic.shift_day sd
    WHERE sd.n = 1 AND sd.d <= :'cutover'::date
    ORDER BY synthetic.rnd('g17a|' || sd."Id") LIMIT 40
), ins AS (
    INSERT INTO "Shift" ("StartDate", "EndDate", "LocationId", "SheriffId", "Timezone", "OvertimeHours", "CreatedOn", "CreatedById")
    SELECT s."StartDate", s."EndDate", s."LocationId", s."SheriffId", s."Timezone", 0, s."CreatedOn" + interval '1 hour', :sys
    FROM "Shift" s JOIN pick USING ("Id") RETURNING "Id", "SheriffId", "StartDate"
)
INSERT INTO synthetic.case_manifest
SELECT 'C-G17A-01', 'G-17', 'Shift', "Id"::text, 'Merged', 'G17_IDENTICAL', 'Identical duplicate shift' FROM ins
UNION ALL
SELECT 'C-G19-01', 'G-19', 'SheriffDay', "SheriffId" || '|' || ("StartDate" AT TIME ZONE 'America/Vancouver')::date,
       'MigratedWithDifference', 'G19_SS_STORED_EXCESS', 'SS stored overtime counts the duplicate twice' FROM ins;

-- G-17 class D: same-location gap day (07:00-12:00 + 13:30-18:00). Past -> report; future -> NeedsAction.
WITH pick AS (
    (SELECT sd."Id", true AS past FROM synthetic.shift_day sd
     WHERE sd.n = 1 AND sd.d <= :'cutover'::date ORDER BY synthetic.rnd('g17d|' || sd."Id") LIMIT 15)
    UNION ALL
    (SELECT sd."Id", false FROM synthetic.shift_day sd
     WHERE sd.n = 1 AND sd.d > :'cutover'::date ORDER BY synthetic.rnd('g17f|' || sd."Id") LIMIT 2)
), up AS (
    UPDATE "Shift" s SET "StartDate" = synthetic.at((s."StartDate" AT TIME ZONE 'America/Vancouver')::date, '07:00'),
                         "EndDate" = synthetic.at((s."StartDate" AT TIME ZONE 'America/Vancouver')::date, '12:00')
    FROM pick WHERE s."Id" = pick."Id" RETURNING s.*, pick.past
), ins AS (
    INSERT INTO "Shift" ("StartDate", "EndDate", "LocationId", "SheriffId", "Timezone", "OvertimeHours", "CreatedOn", "CreatedById")
    SELECT synthetic.at((up."StartDate" AT TIME ZONE 'America/Vancouver')::date, '13:30'),
           synthetic.at((up."StartDate" AT TIME ZONE 'America/Vancouver')::date, '18:00'),
           up."LocationId", up."SheriffId", up."Timezone", 0, up."CreatedOn", :sys
    FROM up RETURNING "Id", "SheriffId", "StartDate"
)
INSERT INTO synthetic.case_manifest
SELECT CASE WHEN up.past THEN 'C-G17D-01' ELSE 'C-G17D-02' END, 'G-17', 'Shift', x."Id"::text,
       CASE WHEN up.past THEN 'MigratedWithDifference' ELSE 'NeedsAction' END,
       CASE WHEN up.past THEN 'G17_GAP_PAST' ELSE 'G17_GAP_FUTURE' END, 'Two shifts with a 90-minute gap at one location'
FROM up JOIN LATERAL (SELECT up."Id" UNION ALL SELECT ins."Id" FROM ins
                      WHERE ins."SheriffId" = up."SheriffId"
                        AND (ins."StartDate" AT TIME ZONE 'America/Vancouver')::date = (up."StartDate" AT TIME ZONE 'America/Vancouver')::date) x("Id") ON true;

-- G-19 cross-location overlap: a second location 15:00-17:00 overlapping the 08:00-16:00 shift by 1 h.
WITH pick AS (
    SELECT sd."Id" FROM synthetic.shift_day sd JOIN "Shift" s ON s."Id" = sd."Id"
    WHERE sd.n = 1 AND sd.d <= :'cutover'::date AND (s."EndDate" - s."StartDate") = interval '8 hours'
    ORDER BY synthetic.rnd('g19x|' || sd."Id") LIMIT 3
), ins AS (
    INSERT INTO "Shift" ("StartDate", "EndDate", "LocationId", "SheriffId", "Timezone", "OvertimeHours", "CreatedOn", "CreatedById")
    SELECT s."StartDate" + interval '7 hours', s."StartDate" + interval '9 hours',
           (SELECT l2.id FROM synthetic.loc l1 JOIN synthetic.loc l2 ON l2.i = 1 + l1.i % 6 WHERE l1.id = s."LocationId"),
           s."SheriffId", s."Timezone", 0, s."CreatedOn", :sys
    FROM "Shift" s JOIN pick USING ("Id") RETURNING "Id", "SheriffId", "StartDate"
)
INSERT INTO synthetic.case_manifest
SELECT 'C-G19-02', 'G-19', 'SheriffDay', "SheriffId" || '|' || ("StartDate" AT TIME ZONE 'America/Vancouver')::date,
       'MigratedWithDifference', 'G19_CROSS_LOCATION_OVERLAP', 'Shifts at two locations overlap by 1 h; SS counts it twice'
FROM ins;

-- G-12e: a staffed past slot split into two segments, same sheriff (merge) or flag-only second half (drop).
WITH single AS (
    SELECT ds."Id", ds."StartDate", ds."EndDate" FROM "DutySlot" ds
    WHERE ds."SheriffId" IS NOT NULL AND ds."StartDate" < synthetic.at(:'cutover'::date, '00:00')
      AND NOT EXISTS (SELECT 1 FROM "DutySlot" o WHERE o."DutyId" = ds."DutyId" AND o."Id" <> ds."Id")
      AND NOT EXISTS (SELECT 1 FROM synthetic.case_manifest m WHERE m.source_table = 'DutySlot' AND m.source_id = ds."Id"::text)
), pick AS (
    SELECT single.*, row_number() OVER (ORDER BY synthetic.rnd('g12e|' || single."Id")) AS rn FROM single
), lim AS (SELECT * FROM pick WHERE rn <= 50),
up AS (
    UPDATE "DutySlot" ds SET "EndDate" = ds."StartDate" + (lim."EndDate" - lim."StartDate") / 2
    FROM lim WHERE ds."Id" = lim."Id" RETURNING ds."Id", ds."DutyId", ds."SheriffId", ds."LocationId", ds."Timezone",
                                                 ds."EndDate" AS mid, lim."EndDate" AS en, lim.rn
), ins AS (
    INSERT INTO "DutySlot" ("StartDate", "EndDate", "DutyId", "SheriffId", "Timezone", "LocationId",
                            "IsNotAvailable", "IsNotRequired", "IsClosed", "IsOvertime", "CreatedOn", "CreatedById")
    SELECT up.mid, up.en, up."DutyId", CASE WHEN up.rn <= 30 THEN up."SheriffId" END, up."Timezone", up."LocationId",
           false, up.rn > 30, false, false, up.mid - interval '7 days', :sys
    FROM up RETURNING "Id", "DutyId", "SheriffId"
)
INSERT INTO synthetic.case_manifest
SELECT 'C-G12E-01', 'G-12e', 'DutySlot', "Id"::text, 'Merged', 'G12E_SAME_SHERIFF', 'Two segments of one duty, same sheriff'
FROM up WHERE rn <= 30
UNION ALL
SELECT 'C-G12E-01', 'G-12e', 'DutySlot', "Id"::text, 'Merged', 'G12E_SAME_SHERIFF', 'Two segments of one duty, same sheriff'
FROM ins WHERE "SheriffId" IS NOT NULL
UNION ALL
SELECT 'C-G12E-02', 'G-12e', 'DutySlot', "Id"::text, 'DroppedBadData', 'G12E_FLAG_SEGMENT', 'Not-required segment beside a sheriff segment'
FROM ins WHERE "SheriffId" IS NULL;

-- G-16: same sheriff twice on one assignment and past date (FTE duty in the long-day overtime shift).
WITH pick AS (
    SELECT DISTINCT ON (ds."SheriffId", lsd.d) ds."SheriffId", du."AssignmentId", du."LocationId", lsd.d, ls."Id" AS shift_id
    FROM "Shift" ls
    JOIN synthetic.shift_day lsd ON lsd."Id" = ls."Id" AND lsd.n = 2
    JOIN "DutySlot" ds ON ds."SheriffId" = ls."SheriffId" AND (ds."StartDate" AT TIME ZONE 'America/Vancouver')::date = lsd.d
    JOIN "Duty" du ON du."Id" = ds."DutyId"
    WHERE (ls."StartDate" AT TIME ZONE 'America/Vancouver')::time = time '16:00' AND lsd.d <= :'cutover'::date
      AND NOT EXISTS (SELECT 1 FROM "DutySlot" o WHERE o."DutyId" = ds."DutyId" AND o."Id" <> ds."Id")
      AND NOT EXISTS (SELECT 1 FROM synthetic.case_manifest m WHERE m.source_table = 'DutySlot' AND m.source_id = ds."Id"::text)
    ORDER BY ds."SheriffId", lsd.d, ds."Id"
), lim AS (SELECT * FROM pick ORDER BY synthetic.rnd('g16|' || shift_id) LIMIT 5),
nd AS (
    INSERT INTO "Duty" ("StartDate", "EndDate", "LocationId", "AssignmentId", "Timezone", "CreatedOn", "CreatedById")
    SELECT synthetic.at(d, '16:00'), synthetic.at(d, '18:00'), "LocationId", "AssignmentId", 'America/Vancouver',
           synthetic.at(d, '00:00') - interval '1 day', :sys
    FROM lim RETURNING "Id", "StartDate", "EndDate", "LocationId", "AssignmentId"
), ns AS (
    INSERT INTO "DutySlot" ("StartDate", "EndDate", "DutyId", "SheriffId", "Timezone", "LocationId",
                            "IsNotAvailable", "IsNotRequired", "IsClosed", "IsOvertime", "CreatedOn", "CreatedById")
    SELECT nd."StartDate", nd."EndDate", nd."Id", lim."SheriffId", 'America/Vancouver', nd."LocationId",
           false, false, false, true, nd."StartDate" - interval '1 day', :sys
    FROM nd JOIN lim ON lim."AssignmentId" = nd."AssignmentId" AND lim.d = (nd."StartDate" AT TIME ZONE 'America/Vancouver')::date
    RETURNING "Id"
)
INSERT INTO synthetic.case_manifest
SELECT 'C-G16-01', 'G-16', 'DutySlot', "Id"::text, 'MigratedWithDifference', 'G16_PAST_CONFLICT',
       'Second entry for the same sheriff, definition and past date'
FROM ns;

-- SP-04: future training overlapping a future shift (overridable calendar conflict).
WITH pick AS (
    SELECT sd."Id" FROM synthetic.shift_day sd
    WHERE sd.n = 1 AND sd.d > :'cutover'::date ORDER BY synthetic.rnd('sp04|' || sd."Id") LIMIT 3
), ins AS (
    INSERT INTO "SheriffTraining" ("TrainingTypeId", "StartDate", "EndDate", "SheriffId", "Timezone", "FirstNotice", "CreatedOn", "CreatedById")
    SELECT 8, s."StartDate" + interval '1 hour', s."StartDate" + interval '4 hours', s."SheriffId", s."Timezone", false, now(), :sys
    FROM "Shift" s JOIN pick USING ("Id") RETURNING "Id"
)
INSERT INTO synthetic.case_manifest
SELECT 'C-SP04-01', 'SP-04', 'SheriffTraining', "Id"::text, 'NeedsAction', 'SP04_FUTURE_CONFLICT',
       'Future training overlaps a future shift' FROM ins;

-- Q-L6 / Q-L4: leave rows using the rejected leave codes.
WITH pick AS (
    SELECT "Id", row_number() OVER (ORDER BY synthetic.rnd('ql|' || "Id")) AS rn FROM "SheriffLeave"
), up AS (
    UPDATE "SheriffLeave" sl SET "LeaveTypeId" = (SELECT "Id" FROM "LookupCode" WHERE "Type" = 5 AND "Code" =
        CASE WHEN pick.rn <= 3 THEN 'Synthetic Unknown Leave' ELSE 'Synthetic Local Leave' END)
    FROM pick WHERE sl."Id" = pick."Id" AND pick.rn <= 5 RETURNING sl."Id", pick.rn
)
INSERT INTO synthetic.case_manifest
SELECT CASE WHEN rn <= 3 THEN 'C-QL6-01' ELSE 'C-QL4-01' END, CASE WHEN rn <= 3 THEN 'Q-L6' ELSE 'Q-L4' END,
       'SheriffLeave', "Id"::text, 'Rejected', CASE WHEN rn <= 3 THEN 'QL6_UNKNOWN_CODE' ELSE 'QL4_SCOPE' END,
       'Leave row whose leave type is rejected'
FROM up;

-- G-17 class C: every touching long-day pair (whole population, not sampled).
INSERT INTO synthetic.case_manifest
SELECT 'C-G17C-01', 'G-17', 'Shift', s."Id"::text, 'Merged', 'G17_TOUCHING', 'Touching shifts on one day merge into one'
FROM "Shift" s
WHERE s."ExpiryDate" IS NULL AND EXISTS (
    SELECT 1 FROM "Shift" o WHERE o."SheriffId" = s."SheriffId" AND o."LocationId" = s."LocationId" AND o."ExpiryDate" IS NULL
      AND o."Id" <> s."Id" AND (o."EndDate" = s."StartDate" OR o."StartDate" = s."EndDate"));

-- ---------------------------------------------------------------------------------------------
-- Background noise: soft deletes and comments. Rows used by a case are left alone.
UPDATE "SheriffLeave" SET "ExpiryDate" = "CreatedOn" + interval '1 day', "ExpiryReason" = 'Synthetic deleted'
WHERE synthetic.rnd('xl|' || "Id") < 0.02
  AND NOT EXISTS (SELECT 1 FROM synthetic.case_manifest m WHERE m.source_table = 'SheriffLeave' AND m.source_id = "Id"::text);
UPDATE "SheriffTraining" SET "ExpiryDate" = "CreatedOn" + interval '1 day', "ExpiryReason" = 'Synthetic deleted'
WHERE synthetic.rnd('xt|' || "Id") < 0.02
  AND NOT EXISTS (SELECT 1 FROM synthetic.case_manifest m WHERE m.source_table = 'SheriffTraining' AND m.source_id = "Id"::text);
UPDATE "SheriffAwayLocation" SET "ExpiryDate" = "CreatedOn" + interval '1 day', "ExpiryReason" = 'Synthetic deleted'
WHERE synthetic.rnd('xa|' || "Id") < 0.02;
WITH x AS (
    SELECT du."Id" FROM "Duty" du
    WHERE synthetic.rnd('xd|' || du."Id") < 0.005
      AND NOT EXISTS (SELECT 1 FROM synthetic.case_manifest m WHERE m.source_table = 'Duty' AND m.source_id = du."Id"::text)
      AND NOT EXISTS (SELECT 1 FROM "DutySlot" ds JOIN synthetic.case_manifest m ON m.source_table = 'DutySlot' AND m.source_id = ds."Id"::text
                      WHERE ds."DutyId" = du."Id")
), d AS (UPDATE "Duty" du SET "ExpiryDate" = du."CreatedOn" + interval '1 day' FROM x WHERE du."Id" = x."Id" RETURNING du."Id")
UPDATE "DutySlot" ds SET "ExpiryDate" = ds."CreatedOn" + interval '1 day' FROM d WHERE ds."DutyId" = d."Id";
UPDATE "Shift" SET "Comment" = 'Synthetic comment ' || "Id" WHERE synthetic.rnd('cs|' || "Id") < 0.05;
UPDATE "Duty" SET "Comment" = 'Synthetic comment ' || "Id" WHERE synthetic.rnd('cd|' || "Id") < 0.03;
UPDATE "SheriffLeave" SET "Comment" = 'Synthetic comment ' || "Id" WHERE synthetic.rnd('cl|' || "Id") < 0.10;
UPDATE "SheriffTraining" SET "Note" = 'Synthetic note ' || "Id" WHERE synthetic.rnd('ct|' || "Id") < 0.10;

-- Stored overtime per the SS rule at write time: per sheriff and local day, sum of live shift hours
-- minus 8, stored on the day's last shift.
WITH s AS (
    SELECT "Id", "SheriffId" AS sid, ("StartDate" AT TIME ZONE 'America/Vancouver')::date AS d,
           extract(epoch FROM "EndDate" - "StartDate") / 3600.0 AS h
    FROM "Shift" WHERE "ExpiryDate" IS NULL
), t AS (SELECT sid, d, greatest(sum(h) - 8.0, 0) AS ot, max("Id") AS last_id FROM s GROUP BY sid, d)
UPDATE "Shift" sh SET "OvertimeHours" = CASE WHEN sh."Id" = t.last_id THEN round(t.ot::numeric, 2) ELSE 0 END
FROM s JOIN t USING (sid, d) WHERE sh."Id" = s."Id";

-- SheriffStandardTraining seed from the SS migration script (needs the Type 6 Ids above).
\if :{?seed_file}
\i :seed_file
\endif

SELECT synthetic.sync_sequences() \g /dev/null
COMMIT;
ANALYZE;

\pset format unaligned
\pset tuples_only on
\pset footer off
\pset fieldsep ' | '
\qecho '# Synthetic SS Database Summary'
\qecho
\qecho 'Parameter | Value'
\qecho '--- | ---'
SELECT k, v FROM synthetic.profile ORDER BY k;
\qecho
\qecho 'Table | Rows | Live rows'
\qecho '--- | ---: | ---:'
SELECT t, n, l FROM (
    SELECT 'Region' t, count(*) n, count(*) FILTER (WHERE "ExpiryDate" IS NULL) l FROM "Region"
    UNION ALL SELECT 'Location', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "Location"
    UNION ALL SELECT 'LookupCode', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "LookupCode"
    UNION ALL SELECT 'User', count(*), count(*) FILTER (WHERE "IsEnabled") FROM "User"
    UNION ALL SELECT 'UserRole', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "UserRole"
    UNION ALL SELECT 'Assignment', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "Assignment"
    UNION ALL SELECT 'Duty', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "Duty"
    UNION ALL SELECT 'DutySlot', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "DutySlot"
    UNION ALL SELECT 'Shift', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "Shift"
    UNION ALL SELECT 'SheriffLeave', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "SheriffLeave"
    UNION ALL SELECT 'SheriffTraining', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "SheriffTraining"
    UNION ALL SELECT 'SheriffAwayLocation', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "SheriffAwayLocation"
    UNION ALL SELECT 'SheriffActingRank', count(*), count(*) FILTER (WHERE "ExpiryDate" IS NULL) FROM "SheriffActingRank"
    UNION ALL SELECT 'SheriffStandardTraining', count(*), count(*) FROM "SheriffStandardTraining"
) x ORDER BY 1;
\qecho
\qecho 'Case | Rule | Source table | Expected outcome | Class | Rows'
\qecho '--- | --- | --- | --- | --- | ---:'
SELECT case_id, rule_ref, source_table, expected_outcome, class_code, count(*)
FROM synthetic.case_manifest GROUP BY 1, 2, 3, 4, 5 ORDER BY 1, 3;
