"""Actual-source structural fingerprints and non-destructive filled-DB refusal."""
import argparse
import hashlib
import json
import subprocess
import sys
from shape_profile import CLOCK, DATABASES, OUT, pg, query


def business_versions(db):
    """Prove excluded photo/Region.Code values were not secretly updated: no value hashes."""
    versions = []
    for table in query(db, """SELECT c.relname AS name FROM pg_class c JOIN pg_namespace n
        ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind='r'
        AND c.relname NOT IN ('Audit','DataProtectionKeys','JcSynchronization',
                             'Permission','RolePermission') ORDER BY c.relname"""):
        name = table["name"]
        versions.append((name, pg(db, f"""SELECT encode(sha256(convert_to(COALESCE(
            jsonb_agg(jsonb_build_array(ctid::text,xmin::text) ORDER BY ctid),
            '[]'::jsonb)::text,'UTF8')),'hex') FROM "{name}";""", True, readonly=True)))
    return versions


def fingerprint(db):
    tables = query(db, """SELECT c.relname AS name FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='public' AND c.relkind='r' ORDER BY c.relname""")
    for table in tables:
        name = table["name"]
        table["count"] = int(pg(db, f'SELECT count(*) FROM "{name}";', True, readonly=True))
        if name in ("Audit", "DataProtectionKeys", "JcSynchronization", "Permission", "RolePermission"):
            table["payload"] = "ExcludedSystemOrPermissionCountOnly"
            continue
        expr = """(to_jsonb(t)-'Photo')||jsonb_build_object(
             '_HasPhoto',t."Photo" IS NOT NULL,'_PhotoLength',octet_length(t."Photo"))""" if name == "User" else "to_jsonb(t)"
        if name == "Region":
            expr = "to_jsonb(t)-'Code'"
        table["digest"] = pg(db, f"""SELECT encode(sha256(convert_to(COALESCE(
            jsonb_agg(v ORDER BY v::text),'[]'::jsonb)::text,'UTF8')),'hex')
            FROM (SELECT {expr} AS v FROM "{name}" t) s;""", True, readonly=True)
    sequences = query(db, """SELECT sequencename,last_value,start_value,increment_by
        FROM pg_sequences WHERE schemaname='public' ORDER BY sequencename""")
    digest = hashlib.sha256(json.dumps({"tables": tables, "sequences": sequences},
                                      sort_keys=True, separators=(",", ":")).encode()).hexdigest().upper()
    value = {"database": db, "fixed_clock": CLOCK, "tables": tables, "sequences": sequences,
             "digest": digest, "photo_bytes_hashed": False,
             "source_region_code_hashed": False,
             "system_audit_permission_payloads_hashed": False}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / f"{db}-fingerprint.json").write_text(json.dumps(value, indent=2), encoding="utf-8")
    return value


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["fingerprint", "compare", "refuse-filled"])
    parser.add_argument("--database", choices=DATABASES, default=DATABASES[0])
    args = parser.parse_args()
    if args.command == "compare":
        a, b = [fingerprint(db) for db in DATABASES]
        if a["digest"] != b["digest"]:
            changed = [x["name"] for x, y in zip(a["tables"], b["tables"]) if x != y]
            raise RuntimeError("Fixed-clock rebuild differs: " + ",".join(changed))
        print("PASS independent rebuild: " + a["digest"])
    elif args.command == "refuse-filled":
        before = fingerprint(args.database)
        versions_before = business_versions(args.database)
        result = subprocess.run(["rtk", "proxy", sys.executable, str(__import__(
            "pathlib").Path(__file__).with_name("shape_profile.py")), "load", "--database", args.database],
            text=True, capture_output=True)
        after = fingerprint(args.database)
        versions_after = business_versions(args.database)
        if result.returncode == 0 or "Refused: filled shape database" not in result.stderr:
            raise RuntimeError("Filled database refusal was not observed")
        if before["digest"] != after["digest"]:
            raise RuntimeError("Filled database refusal changed source footprint")
        if versions_before != versions_after:
            raise RuntimeError("Filled database refusal changed physical business-row versions")
        print("PASS filled database refused before mutation; business ctid/xmin and fingerprint unchanged "
              + after["digest"])
    else:
        print(json.dumps({"database": args.database, "digest": fingerprint(args.database)["digest"]}))


if __name__ == "__main__":
    main()
