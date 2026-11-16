"""Verify actual read receipts and retain limited common-projection differences.

This never reconstructs an SS algorithm, substitutes conflict pairs for availability
events, expands source aliases, or claims full distribution/UI parity.
"""
import collections
import datetime
import hashlib
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2] / "docker/migration/artifacts"
SOURCE = ROOT / "ready-v4-source-all-v1-80e9aae.json"
TARGET = ROOT / "target-report-surfaces-v12.json"
OUTPUT = ROOT / "target-report-surfaces-v12-differences.json"
FIELDS = ("Window", "Surface", "Location", "User", "Kind", "Start", "End")


def key(row):
    return tuple(datetime.datetime.fromisoformat(row[k].replace("Z", "+00:00"))
                 if k in ("Start", "End") and row[k] else row[k] for k in FIELDS)


def main():
    if OUTPUT.exists():
        raise RuntimeError("Retained evidence is immutable")
    if hashlib.sha256(SOURCE.read_bytes()).hexdigest().upper() != (
            "DC28370841D1B444D3950E897EDEE2AA268ACC1EDAD7DFDCCDEECA2B1EDC62C7"):
        raise RuntimeError("Matched source digest differs")
    source = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    target = json.loads(TARGET.read_text(encoding="utf-8-sig"))
    if (source["SourceDatabase"] != "ss_ready_v4" or target["SourceDatabase"] != "ss_ready_v4"
            or target["TargetDatabase"] != "unified_ss_rehearsal_ready_v4_rollback"
            or target["FailedReads"]):
        raise RuntimeError("Wrong provenance or a genuinely failed endpoint read")
    calls = target["Calls"]
    call_keys = {(c["Window"], c["SourceLocationId"]) for c in calls}
    windows = {w["Name"] for w in source["Windows"]}
    locations = {c["SourceLocationId"] for c in calls}
    if (call_keys != {(w, loc) for w in windows for loc in locations}
            or len(call_keys) != len(calls) or not calls
            or any(c["ActualConflictPairs"] is None for c in calls)):
        raise RuntimeError("Actual read coverage is incomplete or contains substituted conflict results")
    expected = []
    for row in source["Rows"]:
        report = row["Report"]
        if report not in ("distribution-conflicts", "distribution-sheriffs"):
            continue
        item = json.loads(row["Key"])
        kind = ("user" if report == "distribution-sheriffs" else
                {"Scheduled": "shift", "Leave": "leave", "AwayLocation": "loanOut",
                 "Training": "training"}[item["kind"]])
        expected.append(dict(Window=row["Window"],
                             Surface="resources" if kind == "user" else "availability-events",
                             Location=str(item["locationId"]), User=str(item["sheriffId"]), Kind=kind,
                             Start=None if kind == "user" else item["start"],
                             End=None if kind == "user" else item["end"]))
    a = collections.Counter(map(key, expected))
    b = collections.Counter(map(key, target["Observations"]))
    differences = []
    for label, rows in (("Missing", a-b), ("Unexpected", b-a)):
        for values, count in rows.items():
            differences.append(dict(Direction=label, Count=count, **dict(zip(
                FIELDS, [x.isoformat() if isinstance(x, datetime.datetime) else x for x in values]))))
    grouped = collections.Counter()
    for d in differences:
        grouped[(d["Direction"], d["Window"], d["Kind"])] += d["Count"]
    document = dict(source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest().upper(),
                    target_sha256=hashlib.sha256(TARGET.read_bytes()).hexdigest().upper(),
                    actual_read_groups=len(calls), actual_conflict_calls=len(calls), failed_reads=0,
                    summary=target["Summary"], differences=differences,
                    full_distribution_parity=False, ui_csv_pdf_executed=False,
                    source_harness_projection_is_ui_evidence=False)
    OUTPUT.write_text(json.dumps(document, indent=2), encoding="utf-8")
    print(json.dumps(dict(actual_read_groups=len(calls), failed_reads=0, summary=target["Summary"],
                          difference_groups=[dict(direction=k[0], window=k[1], kind=k[2], rows=n)
                                             for k, n in grouped.items()]), indent=2))


if __name__ == "__main__":
    main()
