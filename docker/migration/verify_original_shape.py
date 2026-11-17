"""Execute unchanged original SH SQL; compare emitted cells, never replacement SQL.

Inputs and complete output stay in restricted ignored directories. The execution
manifest records exact argv/environment, input hashes, marker and exit status.
No clock substitution: SH-06 retains the original rolling now() window.
"""
import argparse
import collections
import datetime as dt
from decimal import Decimal
import hashlib
import json
import pathlib
import subprocess


ROOT = pathlib.Path(__file__).resolve().parents[2]
INPUT = ROOT / "docker/migration/inputs/original-extractors"
SQL = INPUT / "ss-shape-extractor.sql"
REFERENCE = INPUT / "ss-shape-extracts.md"
SQL_SHA256 = "70C0BB834026A117BF32F8D1FAD7A40F7BC4039BE9BEBFBEE8B8CCC023232920"
TARGETS = {
    "ss_shape_v10": ("unified-ss-migration-shape-source-1", "SS SYNTHETIC SHAPE V10"),
    "ss_shape_v10_rebuild": ("unified-ss-migration-shape-source-1", "SS SYNTHETIC SHAPE V10"),
    "ss_adverse_valid_v18": ("unified-ss-migration-source-1", "SS SYNTHETIC ADVERSE PAGING V18"),
}
KEY_COLUMNS = {
    "SH-01": 2, "SH-02": 2, "SH-03": 2, "SH-04": 2,
    "SH-05": 2, "SH-06": 5, "SH-07": 1, "SH-08": 2,
}
EXPECTED_ROWS = dict(zip(KEY_COLUMNS, (415, 117, 66, 48, 60, 809, 25, 25)))
PGOPTIONS = "-c default_transaction_read_only=on"


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def run(argv):
    return subprocess.run(["rtk", "proxy", *argv], capture_output=True)


def parse(text):
    sections, headers = {}, {}
    section = None
    for line in text.splitlines():
        if line.startswith("## SH-"):
            section = line.split()[1]
            sections[section] = []
        elif section and " | " in line and not line.startswith("---"):
            cells = [x.strip() for x in line.split("|")]
            if section not in headers:
                headers[section] = cells
            else:
                if len(cells) != len(headers[section]):
                    raise ValueError(f"{section}: malformed emitted column count")
                sections[section].append(cells)
    return sections, headers


def compare(reference, actual):
    refs, headers = parse(reference)
    values, actual_headers = parse(actual)
    if {p: len(refs[p]) for p in KEY_COLUMNS} != EXPECTED_ROWS:
        raise ValueError("Original production inventory changed; do not silently change denominator")
    cells, extras, inventory = [], [], {}
    for profile, key_columns in KEY_COLUMNS.items():
        if actual_headers.get(profile) != headers[profile]:
            raise ValueError(profile + ": exact extractor output missing or header mismatch")
        indexes = []
        for rows in (refs[profile], values[profile]):
            index = collections.defaultdict(list)
            for row in rows:
                index[tuple(row[:key_columns])].append(row[key_columns:])
            indexes.append(index)
        expected, observed = indexes
        metric_names = headers[profile][key_columns:]
        inventory[profile] = {
            "production_rows": len(refs[profile]), "actual_rows": len(values[profile]),
            "production_cells": len(refs[profile]) * len(metric_names),
            "production_duplicate_keys": sum(len(x) > 1 for x in expected.values()),
            "actual_duplicate_keys": sum(len(x) > 1 for x in observed.values()),
        }
        for row in refs[profile]:
            key = tuple(row[:key_columns])
            matches = observed.get(key, [])
            for i, (metric, ref) in enumerate(zip(metric_names, row[key_columns:])):
                value = matches[0][i] if len(matches) == 1 else None
                if ref == "(null)":
                    verdict = "NotComparableProductionWithheld"
                elif len(expected[key]) != 1 or len(matches) > 1:
                    verdict = "AmbiguousDuplicateKey"
                elif not matches:
                    verdict = "MissingBucket"
                elif ref == "<5":
                    verdict = ("WithinSuppressedRange" if value == "<5" or
                               (value != "(null)" and 1 <= Decimal(value) <= 4)
                               else "FailSuppressedRange")
                elif value == "(null)":
                    verdict = "ActualWithheld"
                elif value == "<5":
                    # Preserve uncertainty; a suppressed count is not an invented 1.
                    low, high = Decimal(ref) * Decimal(".8"), Decimal(ref) * Decimal("1.2")
                    verdict = ("WithinRange" if low <= 1 and high >= 4
                               else "ActualSuppressedIndeterminate")
                else:
                    low, high = Decimal(ref) * Decimal(".8"), Decimal(ref) * Decimal("1.2")
                    verdict = "WithinRange" if low <= Decimal(value) <= high else "Fail"
                cells.append(dict(profile=profile, key=list(key), metric=metric,
                                  reference=ref, actual=value, verdict=verdict))
        for key in observed.keys() - expected.keys():
            extras.append(dict(profile=profile, key=list(key), rows=observed[key],
                               verdict="UnexpectedBucket"))
    assert len(cells) == 5906
    counts = dict(collections.Counter(c["verdict"] for c in cells))
    per_profile = {
        p: dict(collections.Counter(c["verdict"] for c in cells if c["profile"] == p))
        for p in KEY_COLUMNS
    }
    return {
        "production_cells": len(cells), "counts": counts, "per_profile": per_profile,
        "inventory": inventory, "unexpected_buckets": extras, "cells": cells,
        "accepted": not extras and all(c["verdict"] in {
            "WithinRange", "WithinSuppressedRange", "NotComparableProductionWithheld"
        } for c in cells),
        "withheld_are_not_passes": True,
        "suppressed_denominator": sum(c["reference"] == "<5" for c in cells),
        "numeric_denominator": sum(c["reference"] not in ("<5", "(null)") for c in cells),
        "withheld_denominator": sum(c["reference"] == "(null)" for c in cells),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", choices=TARGETS, required=True)
    parser.add_argument("--label", required=True)
    args = parser.parse_args()
    if not args.label.replace("-", "").isalnum():
        raise ValueError("Simple immutable evidence label required")
    original = SQL.read_bytes()
    if sha(original) != SQL_SHA256:
        raise ValueError("Original SQL hash differs; execution refused")
    reference = REFERENCE.read_bytes()
    container, marker = TARGETS[args.database]
    guard_sql = """SELECT json_build_object('database',current_database(),
      'marker',shobj_description(oid,'pg_database'),
      'read_only',current_setting('transaction_read_only'),
      'server_version',current_setting('server_version'))
      FROM pg_database WHERE datname=current_database()"""
    base = ["docker", "exec", "-e", "PGOPTIONS=" + PGOPTIONS, container,
            "psql", "-X", "-U", "postgres", "-d", args.database]
    guard_result = run([*base, "-At", "-v", "ON_ERROR_STOP=1", "-c", guard_sql])
    if guard_result.returncode:
        raise RuntimeError(guard_result.stderr.decode("utf-8", errors="replace"))
    guard = json.loads(guard_result.stdout)
    if (guard["database"], guard["marker"], guard["read_only"]) != (args.database, marker, "on"):
        raise ValueError("Exact marked read-only isolated source required")
    folder = ROOT / "docker/migration/artifacts" / ("original-sh-" + args.label)
    folder.mkdir(exist_ok=False)
    remote = "/tmp/ss-shape-extractor-" + SQL_SHA256.lower() + ".sql"
    copied = run(["docker", "cp", str(SQL), container + ":" + remote])
    if copied.returncode:
        raise RuntimeError(copied.stderr.decode("utf-8", errors="replace"))
    checksum = run(["docker", "exec", container, "sha256sum", remote])
    if checksum.returncode or checksum.stdout.decode().split()[0].upper() != SQL_SHA256:
        raise ValueError("Container original file hash mismatch")
    command = [*base, "-v", "ON_ERROR_STOP=1", "-f", remote]
    started = dt.datetime.now(dt.timezone.utc).isoformat()
    result = run(command)
    finished = dt.datetime.now(dt.timezone.utc).isoformat()
    (folder / "stdout.md").write_bytes(result.stdout)
    (folder / "stderr.txt").write_bytes(result.stderr)
    manifest = {
        "database": args.database, "container": container, "guard": guard,
        "sql_sha256": SQL_SHA256, "container_sql_sha256": SQL_SHA256,
        "production_markdown_sha256": sha(reference),
        "argv": ["rtk", "proxy", *command], "environment": {"PGOPTIONS": PGOPTIONS},
        "started_utc": started, "finished_utc": finished, "exit_status": result.returncode,
        "stdout_sha256": sha(result.stdout), "stderr_sha256": sha(result.stderr),
        "sql_modified": False, "clock_substituted": False, "query_replaced": False,
    }
    if sha(SQL.read_bytes()) != SQL_SHA256:
        raise ValueError("Original input changed during execution")
    if result.returncode == 0:
        text = result.stdout.decode("utf-8-sig")
        sections, _ = parse(text)
        metadata = dict(sections["SH-00"])
        if metadata["Database"] != args.database or metadata["Transaction read only"] != "on":
            raise ValueError("Original output failed database/read-only metadata checks")
        manifest["emitted_metadata"] = metadata
        comparison = compare(reference.decode("utf-8-sig"), text)
        payload = json.dumps(comparison, indent=2).encode("utf-8")
        (folder / "comparison.json").write_bytes(payload)
        manifest["comparison_sha256"] = sha(payload)
        manifest["comparison_counts"] = comparison["counts"]
        manifest["unexpected_buckets"] = len(comparison["unexpected_buckets"])
        manifest["fit_accepted"] = comparison["accepted"]
    manifest["execution_sha256"] = sha(canonical(manifest))
    (folder / "execution.json").write_bytes(json.dumps(manifest, indent=2).encode("utf-8"))
    print(json.dumps(manifest, indent=2))
    if result.returncode:
        print(result.stderr.decode("utf-8", errors="replace"))
        raise SystemExit(result.returncode)
    if not manifest["fit_accepted"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
