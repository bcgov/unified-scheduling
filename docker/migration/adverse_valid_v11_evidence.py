"""Account actual valid-parent adverse receipts; expectations never become invented outcomes.

Run after Invoke-AdverseValidV11.ps1 -Step Verify. Only exact isolated databases
are read, with connection-default read-only enforcement. Per-case identities stay
in ignored restricted artifacts; stdout contains counts/enumerated finding codes.
"""
import collections
import argparse
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[2]
ARTIFACTS = ROOT / "docker/migration/artifacts"
OUTPUT = ARTIFACTS / "adverse-valid-v13-evidence.json"
TARGET = "unified_ss_rehearsal_adverse_valid_v13"


def read(side, sql):
    database = (("ss_adverse_valid_v18" if TARGET.endswith(("_v18", "_v20")) else "ss_adverse_valid_v11") if side == "source"
                else TARGET)
    command = ["rtk", "proxy", "docker", "exec", "-i", "-e",
               "PGOPTIONS=-c default_transaction_read_only=on",
               f"unified-ss-migration-{side}-1", "psql", "-X", "-qAt",
               "-U", "postgres", "-d", database, "-v", "ON_ERROR_STOP=1"]
    result = subprocess.run(command, input="SELECT COALESCE(json_agg(t),'[]'::json) FROM (" +
                            sql + ") t;", text=True, capture_output=True, check=True)
    return json.loads(result.stdout)


def main():
    if OUTPUT.exists():
        raise RuntimeError("Retained evidence is immutable")
    runs = read("target", '''SELECT "Id","Job","Status","MyTeamRunId","ScannedCount",
        "RejectedCount","FailedCount","SkippedCount","DryRun" FROM "DataMigrationRuns"
        ORDER BY "StartedOn"''')
    latest = {r["Job"]: r for r in runs}
    for job in ("MyTeam", "Training", "Scheduling"):
        if job not in latest or latest[job]["Status"] == "Running":
            raise RuntimeError("Actual child execution must finish before accounting")
    parent = latest["MyTeam"]
    if parent["Status"] != "Completed" or not parent["DryRun"]:
        raise RuntimeError("A genuine completed dry parent is required")
    for job in ("Training", "Scheduling"):
        if latest[job]["MyTeamRunId"] != parent["Id"] or latest[job]["ScannedCount"] <= 0:
            raise RuntimeError("Wrong lineage or unexecuted child")
    outcomes = read("target", '''SELECT "RunId","EntityType","LegacyId","Outcome","Reason"
        FROM "DataMigrationOutcomes"''')
    selections = {
        "MyTeam": [(t, t, "") for t in
                   ("Region", "Location", "Role", "User", "UserRole", "LookupSortOrder",
                    "SheriffActingRank", "SheriffAwayLocation", "SheriffLeave")] +
                  [("PositionType", "LookupCode", 'WHERE "Type"=7'),
                   ("LeaveType", "LookupCode", 'WHERE "Type"=5')],
        "Training": [("Training", "LookupCode", 'WHERE "Type"=6'),
                     ("SheriffTraining", "SheriffTraining", ""),
                     ("SheriffStandardTraining", "SheriffStandardTraining", "")],
        "Scheduling": [(t, t, "") for t in ("Assignment", "Duty", "DutySlot", "Shift")] +
                      [("CourtRoom", "LookupCode", 'WHERE "Type"=0'),
                       ("AssignmentClassification", "LookupCode", 'WHERE "Type" BETWEEN 1 AND 4')]
    }
    accounting = []
    index = {}
    for job, tables in selections.items():
        ledger = [o for o in outcomes if o["RunId"] == latest[job]["Id"]]
        for entity, table, where in tables:
            expected = read("source", f'SELECT "Id"::text AS id FROM "{table}" {where}')
            observed = [o for o in ledger if o["EntityType"] == entity]
            exact = ({r["id"] for r in expected} == {o["LegacyId"] for o in observed}
                     and len(expected) == len(observed))
            accounting.append(dict(job=job, entity=entity, selected=len(expected),
                                   outcomes=len(observed), exact_source_identity_set=exact))
            if not exact:
                raise RuntimeError("Selected-row outcome accounting failed: " + job + "/" + entity)
            for o in observed:
                index[(entity, o["LegacyId"])] = o
        if sum(a["selected"] for a in accounting if a["job"] == job) != latest[job]["ScannedCount"]:
            raise RuntimeError("Actual scanned count does not equal selected business identities")
    graph_path = ARTIFACTS / f'adverse-v11-graph-{latest["Scheduling"]["Id"]}.json'
    graph = json.loads(graph_path.read_text(encoding="utf-8-sig"))
    if (graph["RunId"] != latest["Scheduling"]["Id"]
            or graph["TargetDatabase"] != "unified_ss_rehearsal_adverse_valid_v13"
            or graph["DanglingSchedulingCrosswalks"] != 0
            or graph["DistinctPrivateCreatedTargets"] != graph["InsertedCounter"]):
        raise RuntimeError("Private graph integrity regression")
    links = {(r["EntityType"], r["LegacyId"]): r for r in graph["Links"]}
    members = collections.Counter((r["EntityType"], r["TargetKey"]) for r in graph["Links"])
    shifts = {str(r["Id"]): r for r in graph["Shifts"]}
    cases = read("source", '''SELECT m.case_id,m.source_table,m.source_id,m.expected_outcome,
        m.class_code,c."Type" AS lookup_type FROM synthetic.case_manifest m
        LEFT JOIN "LookupCode" c ON m.source_table='LookupCode' AND c."Id"::text=m.source_id
        ORDER BY m.case_id,m.source_table,m.source_id''')
    repairs = {(r["source_table"], r["source_id"]) for r in
               read("source", "SELECT source_table,source_id FROM synthetic.valid_parent_repairs")}
    if len(cases) != 3388 or len(repairs) != 5:
        raise RuntimeError("Preserved manifest/parent repair inventory differs")
    for c in cases:
        entity = c["source_table"]
        if entity == "LookupCode":
            entity = {0: "CourtRoom", 5: "LeaveType", 6: "Training", 7: "PositionType"}.get(
                c["lookup_type"], "AssignmentClassification")
        o = index.get((entity, c["source_id"]))
        c["actual_outcome"] = o["Outcome"] if o else None
        c["actual_reason"] = o["Reason"] if o else None
        if (c["source_table"], c["source_id"]) in repairs:
            c["classification"] = "SupersededParentRepairOriginalCaseRetained"
        elif c["source_table"] == "SheriffLeave" and c["case_id"] in ("C-QL4-01", "C-QL6-01"):
            c["classification"] = "SupersededDependentOfParentRepair"
        elif entity == "SheriffDay":
            c["classification"] = "LogicalOvertimeExpectationNotIndependentlyVerified"
        elif o is None:
            raise RuntimeError("Executed physical source case has no outcome")
        elif c["expected_outcome"] == "Merged":
            link = links.get((entity, c["source_id"]))
            c["merge_member_count"] = members[(entity, link["TargetKey"])] if link else 0
            c["classification"] = ("StructuralMergeVerified" if
                                   o["Outcome"] in ("Migrated", "Merged", "MigratedWithDifference")
                                   and c["merge_member_count"] >= 2 else "StructuralMergeMismatch")
        elif c["expected_outcome"] == o["Outcome"]:
            c["classification"] = "ExactLedgerOutcomeOnly"
        else:
            c["classification"] = "DifferentOutcomeNeedsReview"
    # Actual private roots, not merely successful row outcomes. Cancelled shifts must
    # reference an existing cancelled event, and every scheduling link must resolve.
    cancelled = sum(r["Status"] == "cancelled" for r in shifts.values())
    expired = read("source", '''SELECT "Id"::text AS id FROM "Shift" WHERE "ExpiryDate" IS NOT NULL''')
    cancellation_proof = collections.Counter()
    for row in expired:
        outcome = index[("Shift", row["id"])]
        if outcome["Outcome"] not in ("Migrated", "Merged", "MigratedWithDifference"):
            cancellation_proof["NotTransferred:" + outcome["Outcome"]] += 1
            continue
        link = links.get(("Shift", row["id"]))
        actual = shifts.get(link["TargetKey"]) if link else None
        if not actual or actual["Status"] != "cancelled":
            raise RuntimeError("Successful expired source shift did not create a real cancelled event")
        cancellation_proof["ActualCancelledEventVerified"] += 1
    groups = collections.Counter((c["case_id"], c["source_table"], c["expected_outcome"],
                                  c["classification"], c["actual_outcome"], c["actual_reason"])
                                 for c in cases)
    summary = dict(collections.Counter(c["classification"] for c in cases))
    document = dict(source="ss_adverse_valid_v11", target="unified_ss_rehearsal_adverse_valid_v13",
                    runs=runs, accounting=accounting, case_summary=summary, cases=cases,
                    cancelled_private_shift_roots=cancelled,
                    expired_source_shift_proof=dict(cancellation_proof),
                    dangling_private_scheduling_crosswalks=graph["DanglingSchedulingCrosswalks"],
                    applied=False, full_adverse_acceptance=False,
                    regression_scope="Actual writers, exact identity accounting, rollback, private graph")
    OUTPUT.write_text(json.dumps(document, indent=2), encoding="utf-8")
    print(json.dumps(dict(runs=runs, accounting=accounting, case_summary=summary,
                          cancelled_private_shift_roots=cancelled,
                          expired_source_shift_proof=dict(cancellation_proof),
                          groups=[dict(case=k[0], table=k[1], expected=k[2], classification=k[3],
                                       actual=k[4], reason=k[5], rows=n) for k, n in groups.items()]),
                     indent=2))
    # Successful accounting is NOT business acceptance. Unverified logical OT
    # expectations and actual mismatches remain explicit, never mapped to a pass.
    if summary.get("StructuralMergeMismatch") or summary.get("DifferentOutcomeNeedsReview"):
        raise RuntimeError("Actual adverse expectation differences retained; no acceptance pass")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", choices=("13", "16", "18", "20"), default="13")
    version = parser.parse_args().version
    TARGET = "unified_ss_rehearsal_adverse_valid_v" + version
    OUTPUT = ARTIFACTS / ("adverse-valid-v" + version + "-evidence.json")
    main()
