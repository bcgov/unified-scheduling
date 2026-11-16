"""Restricted operational evidence: source manifest against actual outcomes, no fake parent."""
import collections
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "docker/migration/artifacts/adverse-v7"


def read(side, sql):
    database = "ss" if side == "source" else "unified_ss_rehearsal_adverse_v7"
    command = ["rtk", "proxy", "docker", "exec", "-i", "-e",
               "PGOPTIONS=-c default_transaction_read_only=on",
               f"unified-ss-migration-{side}-1", "psql", "-X", "-qAt",
               "-U", "postgres", "-d", database, "-v", "ON_ERROR_STOP=1"]
    value = subprocess.run(command, input="SELECT COALESCE(json_agg(t),'[]'::json) FROM (" +
                           sql + ") t;", text=True, capture_output=True, check=True)
    return json.loads(value.stdout)


cases = read("source", """SELECT m.case_id,m.source_table,m.source_id,m.expected_outcome,
    m.class_code,c."Type" AS lookup_type FROM synthetic.case_manifest m
    LEFT JOIN "LookupCode" c ON m.source_table='LookupCode' AND c."Id"::text=m.source_id""")
runs = read("target", '''SELECT "Id","Job","Status","ScannedCount","RejectedCount",
    "FailedCount","SkippedCount" FROM "DataMigrationRuns" ORDER BY "StartedOn"''')
if (not runs or any(r["Job"] != "MyTeam" for r in runs)
        or runs[-1]["Status"] != "Failed" or runs[-1]["RejectedCount"] <= 0):
    raise RuntimeError("Expected failed adverse parent and blocked dependent starts were not observed")
outcomes = read("target", '''SELECT "EntityType","LegacyId","Outcome","Reason"
    FROM "DataMigrationOutcomes" WHERE "RunId"=(SELECT "Id" FROM "DataMigrationRuns"
    WHERE "Job"='MyTeam' ORDER BY "StartedOn" DESC LIMIT 1)''')
index = {(o["EntityType"], o["LegacyId"]): o for o in outcomes}
case_results = []
dependent_entities = {"CourtRoom", "Training", "AssignmentClassification", "Assignment",
                      "Duty", "DutySlot", "Shift", "SheriffTraining", "SheriffStandardTraining",
                      "SheriffDay"}  # Scheduling-only logical manifest group, not a physical table.
for c in cases:
    entity = c["source_table"]
    if entity == "LookupCode":
        entity = {0: "CourtRoom", 5: "LeaveType", 6: "Training", 7: "PositionType"}.get(
            c["lookup_type"], "AssignmentClassification")
    actual = index.get((entity, c["source_id"]))
    if not actual and entity not in dependent_entities:
        raise RuntimeError("Missing MyTeam case outcome cannot be called a blocked dependent")
    c["actual_outcome"] = actual["Outcome"] if actual else None
    c["actual_reason"] = actual["Reason"] if actual else None
    c["classification"] = ("ExactExpectedOutcome" if actual and c["expected_outcome"] == actual["Outcome"]
                           else "DifferentOutcomeNeedsReview" if actual else
                           "NotExecutedDependentLineageBlocked")
    case_results.append(c)
selection = [(t, t, "") for t in ("Region", "Location", "Role", "User", "UserRole")]
selection += [("LeaveType", "LookupCode", 'WHERE "Type"=5'),
              ("PositionType", "LookupCode", 'WHERE "Type"=7'),
              ("LookupSortOrder", "LookupSortOrder", "")]
selection += [(t, t, """WHERE "EndDate">=TIMESTAMPTZ '2015-01-01T08:00:00Z'""")
              for t in ("SheriffActingRank", "SheriffAwayLocation", "SheriffLeave")]
expected = read("source", " UNION ALL ".join(
    f"""SELECT '{entity}' AS entity,count(*) AS n,
    COALESCE(json_agg("Id"::text),'[]'::json) AS source_ids FROM "{table}" {where}"""
    for entity, table, where in selection))
accounting = []
for row in expected:
    observed = [o for o in outcomes if o["EntityType"] == row["entity"]]
    unique = len({o["LegacyId"] for o in observed})
    same_ids = set(row["source_ids"]) == {o["LegacyId"] for o in observed}
    accounting.append({"entity": row["entity"], "n": row["n"], "outcome_rows": len(observed),
                       "unique_source_rows": unique, "exact_source_identity_set": same_ids})
    if row["n"] != unique or len(observed) != unique or not same_ids:
        raise RuntimeError("Adverse selected-row accounting mismatch: " + row["entity"])
if sum(x["n"] for x in accounting) != runs[-1]["ScannedCount"]:
    raise RuntimeError("Adverse run scanned-count mismatch")
exclusions = read("source", """SELECT 'Audit' AS entity,count(*) AS n FROM "Audit"
    UNION ALL SELECT 'JcSynchronization',count(*) FROM "JcSynchronization"
    UNION ALL SELECT 'DataProtectionKeys',count(*) FROM "DataProtectionKeys"
    UNION ALL SELECT 'Permission',count(*) FROM "Permission"
    UNION ALL SELECT 'RolePermission',count(*) FROM "RolePermission" """)
summary = dict(collections.Counter(c["classification"] for c in case_results))
groups = collections.Counter((c["case_id"], c["classification"], c["actual_outcome"],
                              c["actual_reason"]) for c in case_results)
document = {"target": "unified_ss_rehearsal_adverse_v7", "source": "ss",
            "runs": runs, "accounting": accounting, "case_summary": summary,
            "cases": case_results, "excluded_system_permission_counts": exclusions,
            "full_downstream_adverse_acceptance": False}
OUT.mkdir(parents=True, exist_ok=True)
(OUT / "evidence.json").write_text(json.dumps(document, indent=2), encoding="utf-8")
print(json.dumps({"runs": runs, "accounting": accounting, "case_summary": summary,
                  "case_groups": [{"case": k[0], "classification": k[1], "actual": k[2],
                                   "reason": k[3], "rows": n} for k, n in groups.items()],
                  "exclusions": exclusions}, indent=2))
if summary.get("DifferentOutcomeNeedsReview"):
    raise RuntimeError("Executed adverse cases differ; restricted evidence retained, not passed")
