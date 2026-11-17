"""Verify physical fallback in retained real-DML graph; not SS business parity."""
import json
import hashlib
import adverse_valid_v11_evidence as evidence

evidence.TARGET = "unified_ss_rehearsal_adverse_valid_v20"
run = "84955196-f62b-4f1c-a1ab-2a88c911c76a"
path = evidence.ARTIFACTS / f"adverse-v11-graph-{run}.json"
graph = json.loads(path.read_text(encoding="utf-8-sig"))
if (graph["RunId"] != run or graph["SourceDatabase"] != "ss_adverse_valid_v18"
        or graph["TargetDatabase"] != evidence.TARGET):
    raise RuntimeError("Exact retained source/target/run required")
fallbacks = {
    x["LegacyId"] for x in graph["Outcomes"]
    if x["EntityType"] == "AssignmentSeries"
    and x["Outcome"] == "Excluded"
    and x["Reason"] == "StandaloneInconsistentMember"
}
prior = json.loads((evidence.ARTIFACTS / "adverse-valid-v13-causes.json").read_text())
expected = {"g10:assignment:" + str(x["assignment"]) for x in prior["series"]}
if fallbacks != expected:
    raise RuntimeError("Retained rejected membership differs from actual fallback membership")
assignments = sorted(int(x.removeprefix("g10:assignment:")) for x in fallbacks)
source = evidence.read("source", 'SELECT "Id"::text AS id FROM "Duty" WHERE "AssignmentId" IN ('
                       + ",".join(map(str, assignments)) + ")")
duty_ids = {x["id"] for x in source}
mapped = [x for x in graph["Links"] if x["EntityType"] == "Duty" and x["LegacyId"] in duty_ids]
physical = {str(x["Id"]): x for x in graph["Duties"]}
if not mapped or any(x["TargetKey"] not in physical for x in mapped):
    raise RuntimeError("Fallback duty roots missing")
if any(physical[x["TargetKey"]]["AssignmentSeriesId"] is not None
       or physical[x["TargetKey"]]["EventSeriesId"] is not None for x in mapped):
    raise RuntimeError("Partial series attachment survived fallback")
if any(x["EntityType"] == "AssignmentSeries" and x["LegacyId"] in fallbacks
       for x in graph["Links"]):
    raise RuntimeError("Unqualified series crosswalk survived")
print(json.dumps({
    "Proof": "ActualV20StandalonePhysicalFallback",
    "RetainedV13Groups": len(expected),
    "ActualExcludedFallbackGroups": len(fallbacks),
    "MappedRealDutyMembershipsVerifiedStandalone": len(mapped),
    "DistinctRealDutyRoots": len({x["TargetKey"] for x in mapped}),
    "PartialSeriesOrDanglingFallbackLinks": 0,
    "GraphSHA256": hashlib.sha256(path.read_bytes()).hexdigest().upper(),
    "FullBusinessAcceptance": False,
}))
