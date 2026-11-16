-- Exact SH-08 census comparison, not acceptance of unmeasured SH-01..07.
-- Source-only, aggregate-only, no personal text/photographs/credentials in output.
BEGIN READ ONLY;
DO $$
BEGIN
  IF current_database() NOT IN ('ss','ss_ready_v4','ss_ready_v4_probe')
     OR (SELECT shobj_description(oid,'pg_database') FROM pg_database
         WHERE datname=current_database()) IS DISTINCT FROM 'SS SYNTHETIC'
  THEN RAISE EXCEPTION 'Marked named synthetic source required'; END IF;
END $$;
WITH reference(measure,bucket,lo,hi,suppressed) AS (
  VALUES
  ('Discriminator / enabled','Sheriff / disabled',216,216,false),
  ('Discriminator / enabled','Sheriff / enabled',656,656,false),
  ('Discriminator / enabled','User / disabled',1,4,true),
  ('Live roles per user','0',669,669,false),
  ('Live roles per user','1',100,100,false),
  ('Live roles per user','2',97,97,false),
  ('Live roles per user','3',6,6,false),
  ('Live roles per user','4',1,4,true),
  ('Live role held','Administrator',162,162,false),
  ('Live role held','Manager',13,13,false),
  ('Live role held','Reports Supervisor',97,97,false),
  ('Live role held','Sheriff',30,30,false),
  ('Live role held','Super Admin',11,11,false),
  ('Live role held','Types Admin',1,4,true),
  ('Rank text (enabled sheriffs)','Deputy Sheriff',509,509,false),
  ('Rank text (enabled sheriffs)','Inspector',25,25,false),
  ('Rank text (enabled sheriffs)','(other values, each under 5 users)',1,4,true),
  ('Rank text (enabled sheriffs)','Sergeant',75,75,false),
  ('Rank text (enabled sheriffs)','Staff Sergeant',34,34,false),
  ('Rank text (enabled sheriffs)','Superintendent',9,9,false),
  ('Enabled sheriffs per home location (band)','01 1-4',12,12,false),
  ('Enabled sheriffs per home location (band)','02 5-9',11,11,false),
  ('Enabled sheriffs per home location (band)','03 10-24',13,13,false),
  ('Enabled sheriffs per home location (band)','04 25-49',5,5,false),
  ('Enabled sheriffs per home location (band)','05 50-99',1,4,true)
), live_roles AS (
  SELECT ur."UserId",r."Id" AS role_id,r."Name" AS role_name
  FROM "UserRole" ur JOIN "Role" r ON r."Id"=ur."RoleId"
  JOIN "User" u ON u."Id"=ur."UserId" WHERE ur."ExpiryDate" IS NULL
), users_with_roles AS (
  SELECT u.*, (SELECT count(DISTINCT role_id) FROM live_roles r WHERE r."UserId"=u."Id") AS roles
  FROM "User" u
), enabled_sheriffs AS (
  SELECT * FROM "User" WHERE "Discriminator"='Sheriff' AND "IsEnabled"
), homes AS (
  SELECT count(*) AS users FROM enabled_sheriffs WHERE "HomeLocationId" IS NOT NULL GROUP BY "HomeLocationId"
), actual AS (
  SELECT 'Discriminator / enabled'::text AS measure,
    "Discriminator"||' / '||CASE WHEN "IsEnabled" THEN 'enabled' ELSE 'disabled' END AS bucket,
    count(*) AS n FROM "User" GROUP BY 2
  UNION ALL SELECT 'Live roles per user',roles::text,count(*) FROM users_with_roles GROUP BY roles
  UNION ALL SELECT 'Live role held',role_name,count(DISTINCT "UserId") FROM live_roles GROUP BY role_name
  UNION ALL SELECT 'Rank text (enabled sheriffs)',
    CASE WHEN "Rank" IN ('Deputy Sheriff','Inspector','Sergeant','Staff Sergeant','Superintendent')
      THEN "Rank" ELSE '(other values, each under 5 users)' END,count(*)
    FROM enabled_sheriffs GROUP BY 2
  UNION ALL SELECT 'Enabled sheriffs per home location (band)',
    CASE WHEN users<5 THEN '01 1-4' WHEN users<10 THEN '02 5-9'
      WHEN users<25 THEN '03 10-24' WHEN users<50 THEN '04 25-49'
      WHEN users<100 THEN '05 50-99' ELSE 'unprofiled >=100' END,count(*)
    FROM homes GROUP BY 2
), comparison AS (
  SELECT r.*,coalesce(a.n,0) AS actual,
    CASE WHEN suppressed THEN lo ELSE ceil(lo*.8) END AS allowed_min,
    CASE WHEN suppressed THEN hi ELSE floor(hi*1.2) END AS allowed_max
  FROM reference r LEFT JOIN actual a USING(measure,bucket)
)
SELECT 'SH-08' AS profile,measure,bucket,lo AS reference_min,hi AS reference_max,
  suppressed,actual,allowed_min,allowed_max,
  CASE WHEN actual BETWEEN allowed_min AND allowed_max THEN 'WithinRange' ELSE 'Fail' END AS verdict
FROM comparison
UNION ALL
SELECT 'SH-08',a.measure,'(unprofiled values; redacted)',0,0,false,sum(a.n),0,0,'Fail'
FROM actual a LEFT JOIN reference r USING(measure,bucket)
WHERE r.measure IS NULL AND a.n>0 GROUP BY a.measure
ORDER BY measure,bucket;
-- Unknown values are counted as failures but never printed as source text.
ROLLBACK;
