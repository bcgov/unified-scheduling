"""Independent SQL measurements of actual PG source rows, never generator summaries.

The original external SH extractor is not bundled with the baseline. This explicitly
named implementation follows the supplied table definitions; never claim it ran that
missing extractor. Every supplied cell and every unexpected actual bucket is accounted.
"""
import argparse
import collections
import hashlib
import json
import math
import pathlib
from shape_profile import CLOCK, DATABASES, OUT, TABLES, pg, query, references


def measure(db):
    guard = query(db, """SELECT current_database() AS database,
      current_setting('transaction_read_only') AS readonly,
      shobj_description(oid,'pg_database') AS marker
      FROM pg_database WHERE datname=current_database()""")[0]
    if guard != {"database": db, "readonly": "on", "marker": "SS SYNTHETIC SHAPE V10"}:
        raise RuntimeError("Marked, named, read-only shape source required")
    refs = references()
    result = []
    seen = set()

    def cell(profile, key, metric, expected, actual):
        seen.add((profile, tuple(key), metric))
        if expected == "(null)":
            verdict, low, high = "NotComparableSuppressed", None, None
        elif expected == "<5":
            low, high = 1, 4
            verdict = "WithinSuppressedRange" if actual is not None and low <= actual <= high else "Fail"
        else:
            n = float(expected)
            low, high = n * .8, n * 1.2
            # Inclusive float tolerance only for representation, not threshold widening.
            verdict = "WithinRange" if actual is not None and low - 1e-8 <= actual <= high + 1e-8 else "Fail"
        result.append(dict(profile=profile, key=key, metric=metric, reference=expected,
                           actual=actual, low=low, high=high, verdict=verdict))

    def unexpected(profile, key, metric, value):
        if value:
            result.append(dict(profile=profile, key=key, metric=metric, reference="absent",
                               actual=value, low=0, high=0, verdict="Fail"))

    for i, table in enumerate(TABLES, 2):
        home = table in ("SheriffLeave", "SheriffTraining", "SheriffActingRank")
        loc = 'u."HomeLocationId"' if home else 't."LocationId"'
        join = f' LEFT JOIN "User" u ON u."Id"=t."SheriffId"' if home else ""
        a = query(db, f'''SELECT l."Name" AS location,extract(year FROM t."StartDate"
            AT TIME ZONE COALESCE(t."Timezone",'America/Vancouver'))::int AS year,count(*) AS n
            FROM "{table}" t {join} LEFT JOIN "Location" l ON l."Id"={loc}
            WHERE t."ExpiryDate" IS NULL GROUP BY 1,2''')
        actual = {(x["location"], x["year"]): x["n"] for x in a}
        keys = set()
        for r in refs["SH-01"]:
            key = (r[0], int(r[1]))
            keys.add(key)
            cell("SH-01", list(key), table, r[i], actual.get(key, 0))
        for key in actual.keys() - keys:
            unexpected("SH-01", list(key), table, actual[key])
    homes = {x["location"]: x["n"] for x in query(db, '''SELECT l."Name" AS location,count(*) AS n
       FROM "User" u JOIN "Location" l ON l."Id"=u."HomeLocationId"
       WHERE u."IsEnabled" AND u."Discriminator"='Sheriff' GROUP BY 1''')}
    for r in refs["SH-01"]:
        cell("SH-01", [r[0], int(r[1])], "Sheriffs homed", r[-1], homes.get(r[0], 0))
    for location in homes.keys() - {r[0] for r in refs["SH-01"]}:
        unexpected("SH-01", [location], "Sheriffs homed", homes[location])
    for table in TABLES:
        for profile, expression in [
            ("SH-02", '''extract(hour FROM "StartDate" AT TIME ZONE
                         COALESCE("Timezone",'America/Vancouver'))::int'''),
            ("SH-04", '''extract(isodow FROM "StartDate" AT TIME ZONE
                         COALESCE("Timezone",'America/Vancouver'))::int''')
        ]:
            rows = query(db, f'''SELECT {expression} AS bucket,count(*) AS n,
                round(100.0*count(*)/sum(count(*)) OVER(),2) AS share
                FROM "{table}" WHERE "ExpiryDate" IS NULL GROUP BY 1''')
            actual = {str(x["bucket"]): x for x in rows}
            keys = set()
            for r in refs[profile]:
                if r[0] != table:
                    continue
                keys.add(r[1])
                x = actual.get(r[1], {"n": 0, "share": 0})
                cell(profile, [table, r[1]], "Rows", r[2], x["n"])
                cell(profile, [table, r[1]], "Share", r[3], float(x["share"]))
            for key in actual.keys() - keys:
                unexpected(profile, [table, key], "Rows", actual[key]["n"])
        # SQL derives actual bands/medians; generator quota arrays are not read here.
        bands = query(db, f'''SELECT band,count(*) AS n,
            round(100.0*count(*)/sum(count(*)) OVER(),2) AS share,
            percentile_cont(.5) WITHIN GROUP(ORDER BY minutes) AS median FROM (
            SELECT minutes,CASE WHEN minutes<=0 THEN '00 <=0'
              WHEN minutes<60 THEN '01 <1h' WHEN minutes<240 THEN '02 1-4h'
              WHEN minutes<480 THEN '03 4-8h' WHEN minutes=480 THEN '04 =8h'
              WHEN minutes<=510 THEN '05 8-8.5h' WHEN minutes<600 THEN '06 8.5-10h'
              WHEN minutes<720 THEN '07 10-12h' WHEN minutes<1440 THEN '08 12-24h'
              WHEN minutes<2880 THEN '09 1-2d' WHEN minutes<10080 THEN '10 2-7d'
              WHEN minutes<43200 THEN '11 7-30d' ELSE '12 >=30d' END AS band
            FROM (SELECT extract(epoch FROM ("EndDate"-"StartDate"))/60.0 AS minutes
            FROM "{table}" WHERE "ExpiryDate" IS NULL) d) b GROUP BY band''')
        actual = {x["band"]: x for x in bands}
        keys = set()
        for r in refs["SH-03"]:
            if r[0] != table:
                continue
            keys.add(r[1])
            x = actual.get(r[1], {"n": 0, "share": 0, "median": None})
            for metric, ref, value in zip(("Rows", "Share", "Median"), r[2:],
                                         (x["n"], float(x["share"]), x["median"])):
                cell("SH-03", [table, r[1]], metric, ref, value)
        for key in actual.keys() - keys:
            unexpected("SH-03", [table, key], "Rows", actual[key]["n"])
    for table in TABLES:
        if table == "Duty":
            continue
        location = ',count(DISTINCT "LocationId") AS locations' if table == "Shift" else ""
        a = query(db, f'''SELECT year,count(*) AS users,
           percentile_cont(.1) WITHIN GROUP(ORDER BY n) AS p10,
           percentile_cont(.5) WITHIN GROUP(ORDER BY n) AS p50,
           percentile_cont(.9) WITHIN GROUP(ORDER BY n) AS p90 FROM (
           SELECT extract(year FROM "StartDate" AT TIME ZONE
             COALESCE("Timezone",'America/Vancouver'))::int AS year,"SheriffId",count(*) AS n
           FROM "{table}" WHERE "ExpiryDate" IS NULL AND "SheriffId" IS NOT NULL GROUP BY 1,2
           ) s GROUP BY year''')
        by_year = {x["year"]: x for x in a}
        for r in refs["SH-05"]:
            if r[0] == table:
                x = by_year.pop(int(r[1]), None)
                for metric, ref in zip(("users", "p10", "p50", "p90"), r[2:]):
                    cell("SH-05", [table, int(r[1])], metric, ref, x[metric] if x else None)
        for year, x in by_year.items():
            unexpected("SH-05", [table, year], "users", x["users"])
    a = query(db, '''SELECT year,count(*) AS users,
        percentile_cont(.1) WITHIN GROUP(ORDER BY n) AS p10,
        percentile_cont(.5) WITHIN GROUP(ORDER BY n) AS p50,
        percentile_cont(.9) WITHIN GROUP(ORDER BY n) AS p90 FROM (
        SELECT extract(year FROM "StartDate" AT TIME ZONE COALESCE("Timezone",'America/Vancouver'))::int AS year,
        "SheriffId",count(DISTINCT "LocationId") AS n FROM "Shift" WHERE "ExpiryDate" IS NULL GROUP BY 1,2
        ) s GROUP BY year''')
    by_year = {x["year"]: x for x in a}
    for r in refs["SH-05"]:
        if r[0] == "Shift locations":
            x = by_year.pop(int(r[1]), None)
            for metric, ref in zip(("users", "p10", "p50", "p90"), r[2:]):
                cell("SH-05", [r[0], int(r[1])], metric, ref, x[metric] if x else None)
    for year, x in by_year.items():
        unexpected("SH-05", ["Shift locations", year], "users", x["users"])
    patterns = query(db, f'''SELECT c."Type"::text AS type,
        (CASE WHEN a."Monday" THEN 'M' ELSE '-' END ||
         CASE WHEN a."Tuesday" THEN 'T' ELSE '-' END ||
         CASE WHEN a."Wednesday" THEN 'W' ELSE '-' END ||
         CASE WHEN a."Thursday" THEN 'T' ELSE '-' END ||
         CASE WHEN a."Friday" THEN 'F' ELSE '-' END ||
         CASE WHEN a."Saturday" THEN 'S' ELSE '-' END ||
         CASE WHEN a."Sunday" THEN 'S' ELSE '-' END) AS mask,
        to_char(a."Start",'HH24:MI') AS start,to_char(a."End",'HH24:MI') AS end,
        CASE WHEN a."AdhocStartDate" IS NULL THEN 'no' ELSE 'yes' END AS adhoc,
        count(*) AS n, sum((SELECT count(*) FROM "Duty" d WHERE d."AssignmentId"=a."Id"
          AND d."ExpiryDate" IS NULL AND d."StartDate">=TIMESTAMPTZ '{CLOCK}'-interval '1year'
          AND d."StartDate"<=TIMESTAMPTZ '{CLOCK}')) AS duties
        FROM "Assignment" a JOIN "LookupCode" c ON c."Id"=a."LookupCodeId"
        WHERE a."ExpiryDate" IS NULL GROUP BY 1,2,3,4,5''')
    actual = {(x["type"], x["mask"], x["start"], x["end"], x["adhoc"]): x for x in patterns}
    for r in refs["SH-06"]:
        key = tuple(r[:5])
        x = actual.pop(key, {"n": 0, "duties": 0})
        cell("SH-06", list(key), "Assignments", r[5], x["n"])
        cell("SH-06", list(key), "Live duties last12months", r[6], x["duties"])
    for key, x in actual.items():
        unexpected("SH-06", list(key), "Assignments", x["n"])
    for r in refs["SH-07"]:
        field, *mode = r[0].split(" ")
        table, column = field.split(".")
        if mode and mode[0] == "(true)":
            expr = f'''CASE WHEN "{column}" THEN 'true' ELSE NULL END'''
        elif mode and mode[0] == "(bytes)":
            expr = f'''CASE WHEN "{column}" IS NULL THEN NULL
                       ELSE repeat('x',octet_length("{column}")) END'''
        else:
            expr = f'"{column}"::text'
        # Text is used only inside aggregate SQL, never returned as row data.
        actual = query(db, f'''SELECT count(*) AS rows,
            count(*) FILTER(WHERE v IS NULL) AS null,
            count(*) FILTER(WHERE v IS NOT NULL AND btrim(v)='') AS blank,
            count(*) FILTER(WHERE btrim(v)<>'' AND length(v) BETWEEN 1 AND 20) AS short,
            count(*) FILTER(WHERE btrim(v)<>'' AND length(v) BETWEEN 21 AND 100) AS medium,
            count(*) FILTER(WHERE btrim(v)<>'' AND length(v) BETWEEN 101 AND 500) AS long,
            count(*) FILTER(WHERE btrim(v)<>'' AND length(v)>500) AS huge
            FROM (SELECT {expr} AS v FROM "{table}") t''')[0]
        for metric, ref in zip(("rows", "null", "blank", "short", "medium", "long", "huge"), r[1:]):
            cell("SH-07", [r[0]], metric, ref, actual[metric])
    census = {}
    for x in query(db, '''SELECT "Discriminator"||' / '||
        CASE WHEN "IsEnabled" THEN 'enabled' ELSE 'disabled' END AS value,count(*) AS n FROM "User" GROUP BY 1'''):
        census["Discriminator / enabled", x["value"]] = x["n"]
    for x in query(db, '''SELECT n::text AS value,count(*) AS n FROM (
        SELECT u."Id",count(DISTINCT r."Id") AS n FROM "User" u LEFT JOIN "UserRole" ur ON
        ur."UserId"=u."Id" AND ur."ExpiryDate" IS NULL LEFT JOIN "Role" r ON r."Id"=ur."RoleId"
        GROUP BY u."Id") t GROUP BY 1'''):
        census["Live roles per user", x["value"]] = x["n"]
    for x in query(db, '''SELECT r."Name" AS value,count(DISTINCT ur."UserId") AS n FROM "UserRole" ur
        JOIN "User" u ON u."Id"=ur."UserId" JOIN "Role" r ON r."Id"=ur."RoleId"
        WHERE ur."ExpiryDate" IS NULL GROUP BY 1'''):
        census["Live role held", x["value"]] = x["n"]
    ranks = query(db, '''SELECT "Rank" AS value,count(*) AS n FROM "User"
        WHERE "Discriminator"='Sheriff' AND "IsEnabled" GROUP BY 1''')
    for x in ranks:
        key = x["value"] if x["n"] >= 5 else "(other values, each under 5 users)"
        census["Rank text (enabled sheriffs)", key] = census.get(
            ("Rank text (enabled sheriffs)", key), 0) + x["n"]
    for x in query(db, '''SELECT CASE WHEN n<5 THEN '01 1-4' WHEN n<10 THEN '02 5-9'
        WHEN n<25 THEN '03 10-24' WHEN n<50 THEN '04 25-49' WHEN n<100 THEN '05 50-99'
        ELSE '06 >=100' END AS value,count(*) AS n FROM (
        SELECT "HomeLocationId",count(*) AS n FROM "User" WHERE "Discriminator"='Sheriff'
        AND "IsEnabled" AND "HomeLocationId" IS NOT NULL GROUP BY 1) t GROUP BY 1'''):
        census["Enabled sheriffs per home location (band)", x["value"]] = x["n"]
    for r in refs["SH-08"]:
        cell("SH-08", r[:2], "Users", r[2], census.pop(tuple(r[:2]), 0))
    for key, n in census.items():
        unexpected("SH-08", list(key), "Users", n)
    summary = {profile: dict(collections.Counter(
        x["verdict"] for x in result if x["profile"] == profile)) for profile in refs if profile != "SH-00"}
    inventory = query(db, """SELECT c.relname AS table,(SELECT count(*) FROM pg_attribute a
        WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped) AS columns
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public'
        AND c.relkind='r' ORDER BY c.relname""")
    for table in inventory:
        table["rows"] = int(pg(db, f'SELECT count(*) FROM "{table["table"]}";', True))
    failures = [r for r in result if r["verdict"] == "Fail"]
    document = {"database": db, "fixed_clock": CLOCK, "measurement": "repo-local independent SH definitions",
                "original_external_extractors_executed": False, "summary": summary,
                "inventory": inventory, "cells": result, "failed_cells": len(failures)}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / f"{db}-fit.json").write_text(json.dumps(document, indent=2), encoding="utf-8")
    print(json.dumps({"database": db, "summary": summary, "failed_cells": len(failures),
                      "first_failures": failures[:25]}, indent=2))
    return len(failures)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", choices=DATABASES, default=DATABASES[0])
    raise SystemExit(1 if measure(parser.parse_args().database) else 0)
