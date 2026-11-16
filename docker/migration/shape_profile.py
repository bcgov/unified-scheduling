"""Aggregate-only SS fitted profile. No production connection, rows, or target writes.

Use the fresh, exact-name PG12 shape container. No host ports/credentials needed.
Public reference location labels are from the supplied aggregate, not private rows.
Generation and independent SQL measurement are deliberately separate.
"""
import argparse
import collections
import datetime as dt
import hashlib
import json
import math
import os
import pathlib
import random
import subprocess
import sys
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
TOOLS = ROOT / "docker/migration/artifacts/shape-tools"
sys.path.insert(0, str(TOOLS))
import numpy as np
from scipy.optimize import Bounds, LinearConstraint, milp
from scipy.sparse import lil_matrix

EVIDENCE = ROOT / "docs/data-migration/ss-implementation-baseline/evidence"
AGGREGATE = EVIDENCE / "ss-shape-extracts.md"
OUT = ROOT / "docker/migration/artifacts/shape-v10"
CLOCK = "2026-11-16T05:59:21+00:00"
SEED = "ss-shape-v9-20261116"
CONTAINER = "unified-ss-migration-shape-source-1"
DATABASES = ("ss_shape_v10", "ss_shape_v10_rebuild")
TABLES = ["Shift", "Duty", "DutySlot", "SheriffLeave", "SheriffTraining",
          "SheriffAwayLocation", "SheriffActingRank"]
DAY_NAMES = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]
UTC = dt.timezone.utc
SYS = "00000000-0000-0000-0000-000000000001"


def pg(database, sql, tuples=False, readonly=False):
    if database not in (*DATABASES, "postgres"):
        raise ValueError("Exact isolated shape database required")
    cmd = ["rtk", "proxy", "docker", "exec", "-i"]
    if readonly:
        cmd += ["-e", "PGOPTIONS=-c default_transaction_read_only=on"]
    cmd += [CONTAINER, "psql", "-X", "-q",
           "-U", "postgres", "-d", database, "-v", "ON_ERROR_STOP=1", "-P", "pager=off"]
    if tuples:
        cmd += ["-At"]
    result = subprocess.run(cmd, input=sql, text=True, encoding="utf-8", capture_output=True)
    if result.returncode:
        # All data here is generated, but do not print PostgreSQL row/detail payloads.
        raise RuntimeError("Shape PostgreSQL command failed: " +
                           "\n".join(x for x in result.stderr.splitlines()
                                     if "ERROR:" in x or "WARNING:" in x))
    return result.stdout.strip()


def query(db, sql):
    return json.loads(pg(db, "SELECT COALESCE(json_agg(t),'[]'::json) FROM (" + sql + ") t;",
                         True, readonly=True))


def references():
    sections = {}
    section = None
    for line in AGGREGATE.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("## SH-"):
            section = line.split()[1]
            sections[section] = []
        elif section and " | " in line and not line.startswith(
            ("---", "Location |", "Table |", "Lookup type |", "Table.Column |", "Measure |", "Item |")
        ):
            sections[section].append([x.strip() for x in line.split("|")])
    assert {k: len(v) for k, v in sections.items()} == {
        "SH-00": 5, "SH-01": 415, "SH-02": 117, "SH-03": 66, "SH-04": 48,
        "SH-05": 60, "SH-06": 809, "SH-07": 25, "SH-08": 25}
    return sections


def count(value):
    return 1 if value == "<5" else int(value)


def identifier(value):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, SEED + ":" + str(value)))


def bag(items, total, key):
    """Largest-remainder apportionment of GENERATOR quotas, not measured output."""
    weights = np.array([count(v) for _, v in items], dtype=float)
    exact = weights * total / weights.sum()
    sizes = np.floor(exact).astype(int)
    for i in sorted(range(len(items)), key=lambda i: (-(exact[i] - sizes[i]), i))[:total - sizes.sum()]:
        sizes[i] += 1
    values = [items[i][0] for i, size in enumerate(sizes) for _ in range(size)]
    random.Random(SEED + key).shuffle(values)
    return values


def workload(groups, capacities, target, total, fractional=False):
    """Integer transport: actual rows/group, active users, bounded order statistics.

    Unknown tails are invented; measured medians/percentiles are never filled in.
    Rows stay in the requested location/year; disabled users can carry real history.
    """
    user_count, *quantiles = target
    levels = set(range(1, 21)) | {25, 30, 40, 50, 60, 75, 90, 100, 125, 150,
                                175, 200, 250, 300, 400, 500, 750, 1000, 1500, 2500, 5000}
    for value in quantiles:
        levels.update([max(1, math.floor(value * .8)), math.ceil(value * 1.2),
                       max(1, round(value)), max(1, math.floor(value)), math.ceil(value)])
    variables = [(g, n) for g, size in groups.items() if size
                 for n in sorted(levels | {size}) if n <= size]
    for users in [user_count] + sorted(
        (n for n in range(math.ceil(user_count * .8), math.floor(user_count * 1.2) + 1)
         if n != user_count), key=lambda n: (abs(n - user_count), n)
    ):
        constraints = []
        for g, size in groups.items():
            if size:
                constraints += [(lambda a, g=g: a[1] if a[0] == g else 0, size, size),
                                (lambda a, g=g: int(a[0] == g), 0, capacities[g])]
        constraints.append((lambda a: 1, users, users))
        for p, q in zip((.1, .5, .9), quantiles):
            low, high = math.ceil(.8 * q), math.floor(1.2 * q)
            pos = (users - 1) * p
            k, upper = math.floor(pos), math.ceil(pos)
            interpolated = math.floor(q) + (pos - k) * (math.ceil(q) - math.floor(q))
            fractional_pair = (fractional and q != int(q) and k != upper and
                               .8 * q <= interpolated <= 1.2 * q)
            if low <= high and not fractional_pair:
                constraints += [(lambda a, low=low: int(a[1] < low), 0, k),
                                (lambda a, high=high: int(a[1] <= high), upper + 1, users)]
            else:
                # e.g. p10=1.5 with26 users: the third/fourth order values are1/2.
                a, b = math.floor(q), math.ceil(q)
                interpolated = a + (pos - k) * (b - a)
                if not .8 * q <= interpolated <= 1.2 * q:
                    break
                constraints += [(lambda v, a=a: int(v[1] < a), 0, k),
                                (lambda v, a=a: int(v[1] <= a), k + 1, k + 1),
                                (lambda v, b=b: int(v[1] <= b), upper + 1, users)]
        else:
            matrix = lil_matrix((len(constraints), len(variables)), dtype=float)
            for i, (fn, lo, hi) in enumerate(constraints):
                matrix[i] = [fn(v) for v in variables]
            # Convex tail cost avoids needlessly enormous per-person workloads.
            costs = np.array([n * n / max(total, 1) for _, n in variables])
            result = milp(costs, integrality=np.ones(len(variables)),
                          bounds=Bounds(0, np.inf),
                          constraints=LinearConstraint(matrix.tocsc(),
                                                       [r[1] for r in constraints],
                                                       [r[2] for r in constraints]),
                          options={"node_limit": 500, "mip_rel_gap": .01})
            if result.x is not None:
                allocation = collections.defaultdict(list)
                for (g, n), value in zip(variables, np.rint(result.x).astype(int)):
                    allocation[g].extend([n] * value)
                # Time-limited feasible solutions are accepted ONLY after exact checks.
                vector = np.rint(result.x)
                measured = matrix @ vector
                if any(v < lo - .01 or v > hi + .01
                       for v, (_, lo, hi) in zip(measured, constraints)):
                    continue
                return allocation
    if not fractional:
        return workload(groups, capacities, target, total, fractional=True)
    raise RuntimeError(f"No feasible source workload allocation: {target}, total={total}")


def local_date(year, weekday, index):
    first = dt.date(year, 1, 1)
    first += dt.timedelta(days=(weekday - first.isoweekday()) % 7)
    dates = []
    d = first
    while d.year == year:
        # Avoid ambiguous/nonexistent DST local hours, not real production rows.
        if not (d.month in (3, 11) and d.weekday() == 6):
            dates.append(d)
        d += dt.timedelta(days=7)
    return dates[index % len(dates)]


def source_zone():
    # PostgreSQL12's actual rules, NOT the host's newer/different future BC rules.
    from zoneinfo import ZoneInfo
    with (OUT / "Vancouver.tzif").open("rb") as stream:
        return ZoneInfo.from_file(stream, key="SourcePostgreSQL12/America/Vancouver")


SOURCE_ZONE = None


def timestamp(date, hour):
    global SOURCE_ZONE
    if SOURCE_ZONE is None:
        SOURCE_ZONE = source_zone()
    return dt.datetime.combine(date, dt.time(hour)).replace(
        tzinfo=SOURCE_ZONE).astimezone(UTC)


def generate():
    r = references()
    data = collections.defaultdict(list)
    locations = sorted({x[0] for x in r["SH-01"]})
    loc_id = {name: 10000 + i for i, name in enumerate(locations)}
    enabled = {x[0]: count(x[-1]) for x in r["SH-01"]}
    suppressed = sorted({x[0] for x in r["SH-01"] if x[-1] == "<5"})
    left = 656 - sum(enabled.values())
    for name in suppressed:
        add = min(3, left)
        enabled[name] += add
        left -= add
    assert left == 0 and sum(enabled.values()) == 656
    data["Region"] = [{"Id": 10000, "Name": "Synthetic Shape Region",
                       "Code": "SHAPE", "JustinId": 990001}]
    for name in locations:
        data["Location"].append({"Id": loc_id[name], "Name": name,
                                 "AgencyId": f"shape-{loc_id[name]}",
                                 "JustinCode": f"S{loc_id[name]}", "RegionId": 10000,
                                 "Timezone": "America/Vancouver"})
    users = []
    home_pool = collections.defaultdict(list)
    rank_bag = (["Deputy Sheriff"] * 509 + ["Inspector"] * 25 + ["Sergeant"] * 75 +
                ["Staff Sergeant"] * 34 + ["Superintendent"] * 9 + ["Constable Sheriff"] * 4)
    for name in locations:
        for _ in range(enabled[name]):
            i = len(users) + 1
            user = {"Id": identifier(i), "HomeLocationId": loc_id[name],
                    "Discriminator": "Sheriff", "IsEnabled": True, "Rank": rank_bag[i - 1]}
            users.append(user)
            home_pool[name].append(user["Id"])
    # Historical users at every home, including currently unstaffed locations.
    for i in range(216):
        name = locations[i % len(locations)]
        user = {"Id": identifier(657 + i), "HomeLocationId": loc_id[name],
                "Discriminator": "Sheriff", "IsEnabled": False, "Rank": "Deputy Sheriff"}
        users.append(user)
        home_pool[name].append(user["Id"])
    users.append({"Id": SYS, "Discriminator": "User", "IsEnabled": False})
    for i, user in enumerate(users):
        user.update({"FirstName": "Shape", "LastName": f"Person{i:04}",
                     "IdirName": f"shape{i:04}" if i < 872 else None,
                     "Email": (f"u{i}@example.invalid" if i < 130 else
                               f"shape.person{i:04}@example.invalid") if i < 872 else None,
                     "BadgeNumber": f"S{i:04}" if i < 872 else None,
                     "Gender": 2 if i < 869 else None, "Excused": i < 38,
                     "IdirId": identifier(f"idir{i}") if i < 331 else None,
                     "KeyCloakId": identifier(f"sso{i}") if i < 332 else None,
                     "LastLogin": CLOCK if i < 332 else None,
                     "_GeneratePhoto": i < 494,
                     "LastPhotoUpdate": CLOCK if i < 494 else None})
    data["User"] = users
    # Degree-preserving simple bipartite role graph: no duplicate user/role edges.
    role_degrees = {"Administrator": 162, "Manager": 13, "Reports Supervisor": 97,
                    "Sheriff": 30, "Super Admin": 11, "Types Admin": 3}
    role_ids = {name: 10000 + i for i, name in enumerate(role_degrees)}
    data["Role"] = [{"Id": role_ids[n], "Name": n} for n in role_degrees]
    for user, degree in zip(users, [4] + [3] * 6 + [2] * 97 + [1] * 100):
        chosen = sorted(role_degrees, key=lambda n: (-role_degrees[n], n))[:degree]
        assert all(role_degrees[n] for n in chosen)
        for name in chosen:
            data["UserRole"].append({"UserId": user["Id"], "RoleId": role_ids[name],
                                     "EffectiveDate": "2015-01-01T08:00:00+00:00"})
            role_degrees[name] -= 1
    assert not any(role_degrees.values())
    # Valid public lookup identities for the actual source schema.
    for t, code in enumerate(["Courtroom", "Court Sergeant", "Cells", "Escort", "Administration"]):
        data["LookupCode"].append({"Id": 10000 + t, "Type": t, "Code": code,
                                   "Description": code,
                                   "LocationId": loc_id[locations[0]] if t == 0 else None})
    data["LookupCode"] += [{"Id": 10005, "Type": 5, "Code": "A/L"},
                           {"Id": 10006, "Type": 6, "Code": "Synthetic Shape Course",
                            "Category": "Synthetic Shape"}]
    for i, name in enumerate(locations):
        data["LookupCode"].append({"Id": 20000 + i, "Type": 0, "Code": "Shape Courtroom",
                                   "LocationId": loc_id[name]})
    # Exact reference pattern keys, invented cardinalities for suppressed1..4.
    patterns = r["SH-06"]
    sizes = [count(x[5]) for x in patterns]
    # All-row assignment field inventory has2767 rows;1138 expired implies1629 live.
    extra = 1629 - sum(sizes)
    for i, pattern in enumerate(patterns):
        if pattern[5] == "<5" and extra:
            n = min(extra, 3)
            sizes[i] += n
            extra -= n
    assert extra == 0
    assignment_by_pattern = []
    for i, (pattern, size) in enumerate(zip(patterns, sizes)):
        ids = []
        for j in range(size):
            aid = 10000 + len(data["Assignment"])
            start, end = pattern[2:4]
            a = {"Id": aid, "LookupCodeId": 10000 + int(pattern[0]),
                 "Start": start, "End": end, "LocationId": loc_id[locations[(i + j) % len(locations)]],
                 "AdhocStartDate": "2026-01-01T08:00:00+00:00" if pattern[4] == "yes" else None,
                 "AdhocEndDate": "2026-01-02T08:00:00+00:00" if pattern[4] == "yes" else None}
            a.update({day: flag != "-" for day, flag in zip(DAY_NAMES, pattern[1])})
            data["Assignment"].append(a)
            ids.append(aid)
        assignment_by_pattern.append(ids)
    for i in range(1138):
        data["Assignment"].append({"Id": 10000 + len(data["Assignment"]),
                                   "LookupCodeId": 10004, "Start": "08:00", "End": "16:00",
                                   "LocationId": loc_id[locations[i % len(locations)]],
                                   "ExpiryDate": CLOCK})
    workload_ref = {(x[0], int(x[1])): x for x in r["SH-05"] if x[2] != "<5"}
    expired_assignments = {a["LocationId"]: a["Id"] for a in data["Assignment"] if a.get("ExpiryDate")}
    for col, table in enumerate(TABLES, 2):
        groups_by_year = collections.defaultdict(dict)
        for row in r["SH-01"]:
            if count(row[col]):
                groups_by_year[int(row[1])][row[0]] = count(row[col])
        for year, groups in groups_by_year.items():
            ref = workload_ref.get((table, year))
            if not ref:
                continue
            minimum_users = max(len(groups), math.ceil(int(ref[2]) * .8))
            minimum_rows = minimum_users + (minimum_users - math.floor((minimum_users - 1) * .9)) * (
                max(1, math.ceil(.8 * float(ref[5]))) - 1)
            extra = max(0, minimum_rows - sum(groups.values()))
            for size in (1, 2, 3):
                for source_row in r["SH-01"]:
                    if (int(source_row[1]) == year and source_row[col] == "<5"
                            and groups[source_row[0]] == size and extra):
                        groups[source_row[0]] += 1
                        extra -= 1
            if extra:
                raise RuntimeError("Suppressed input intervals cannot fit workload constraints")
        total_live = sum(sum(g.values()) for g in groups_by_year.values())
        hours = bag([(int(x[1]), x[2]) for x in r["SH-02"] if x[0] == table],
                    total_live, table + "-hour")
        weekdays = bag([(int(x[1]), x[2]) for x in r["SH-04"] if x[0] == table],
                       total_live, table + "-weekday")
        suppressed_medians = [0, 30, 120, 420, 480, 495, 540, 630, 720, 2000, 5000, 15000, 50000]
        durations = bag([(float(x[4]) if x[4] != "(null)" else
                          suppressed_medians[int(x[1][:2])], x[2])
                         for x in r["SH-03"] if x[0] == table],
                        total_live, table + "-duration")
        if table == "DutySlot":
            # Long slots belong to ordinary full-day hours, short slots to fringe hours.
            # Preserve both complete marginals; do not generate independent nonsensical tails.
            priority = {h: i for i, h in enumerate((8, 9, 7, 6, 10, 5, 11, 12, 13, 14,
                                                   15, 16, 17, 18, 19, 20, 4, 1, 0))}
            ordered = sorted(range(total_live), key=lambda i: (priority.get(hours[i], 99), i))
            for i, duration in zip(ordered, sorted(durations, reverse=True)):
                durations[i] = duration
        cursor = 0
        for year, groups in sorted(groups_by_year.items()):
            n = sum(groups.values())
            print(f"Allocate {table}/{year}: {n}", flush=True)
            ref = workload_ref.get((table, year))
            home = table in ("SheriffTraining", "SheriffLeave", "SheriffActingRank")
            capacities = {g: len(home_pool[g]) if home else 872 for g in groups}
            if ref:
                allocation = workload(groups, capacities,
                                      [int(ref[2])] + [float(x) for x in ref[3:]], n)
            else:
                allocation = {g: [size] for g, size in groups.items()}
            sheriff_offset = 0
            year_rows = []
            for name in sorted(groups):
                counts = allocation[name]
                sheriffs = home_pool[name] if home else [u["Id"] for u in users[:-1]][
                    sheriff_offset:sheriff_offset + len(counts)]
                assert len(sheriffs) >= len(counts)
                sheriff_offset += len(counts)
                sheriff_bag = [s for s, k in zip(sheriffs, counts) for _ in range(k)]
                random.Random(SEED + table + str(year) + name).shuffle(sheriff_bag)
                for j, sheriff in enumerate(sheriff_bag):
                    weekday, hour, duration = weekdays[cursor], hours[cursor], durations[cursor]
                    date = local_date(year, weekday, j + cursor)
                    start = timestamp(date, hour)
                    row = {"Id": 10000 + len(data[table]) + len(year_rows),
                           "StartDate": start.isoformat(),
                           "EndDate": (start + dt.timedelta(minutes=duration)).isoformat(),
                           "SheriffId": sheriff, "LocationId": loc_id[name],
                           "Timezone": "America/Vancouver"}
                    if table == "Duty":
                        row.pop("SheriffId")
                        row["AssignmentId"] = expired_assignments[loc_id[name]]
                    if table == "DutySlot":
                        row["DutyId"] = None  # resolved against real generated parent below
                    if table == "SheriffLeave":
                        row["LeaveTypeId"] = 10005
                    if table == "SheriffTraining":
                        row["TrainingTypeId"] = 10006
                    if table == "SheriffActingRank":
                        row["Rank"] = "Deputy Sheriff"
                        row.pop("LocationId")
                    year_rows.append(row)
                    cursor += 1
            # Exact per-user rows preserved; cross-location swaps only for the measured2-location tail.
            if table == "Shift" and year in (2024, 2025, 2026):
                by_user = collections.defaultdict(list)
                for row in year_rows:
                    by_user[row["SheriffId"]].append(row)
                eligible = [v for _, v in sorted(by_user.items()) if len(v) > 1]
                needed = math.ceil(len(by_user) * .2)
                changed = set()
                for a in eligible:
                    if a[0]["SheriffId"] in changed:
                        continue
                    b = next((b for b in eligible if b[0]["SheriffId"] not in changed
                              and b[0]["LocationId"] != a[0]["LocationId"]), None)
                    if b:
                        changed.update((a[0]["SheriffId"], b[0]["SheriffId"]))
                        a[0]["SheriffId"], b[0]["SheriffId"] = b[0]["SheriffId"], a[0]["SheriffId"]
                    if len(changed) >= needed:
                        break
            data[table].extend(year_rows)
        # SH07 covers all rows, not just live. Expired rows are actual rows and kept in the source.
        field_ref = next((x for x in r["SH-07"] if x[0] == table + ".Comment"), None)
        all_count = int(field_ref[1]) if field_ref else len(data[table])
        for j in range(all_count - len(data[table])):
            row = dict(data[table][j % total_live])
            row.update({"Id": 10000 + len(data[table]), "ExpiryDate": CLOCK})
            data[table].append(row)
    # Relationships are physical FKs, never report-only count records.
    # Live duty counts by actual assignment pattern over the fixed last12months.
    recent = [row for row in data["Duty"] if not row.get("ExpiryDate")
              and "2025-11-16T05:59:21" <= row["StartDate"] <= CLOCK]
    required = sum(count(x[6]) for x in patterns)
    pool_target = required + max(500, required // 10)
    if len(recent) < pool_target:
        # Change source generation dates, not the measurement window/threshold.
        recent_ids = {row["Id"] for row in recent}
        more = [row for row in data["Duty"] if not row.get("ExpiryDate")
                and row["StartDate"].startswith("2025") and row["Id"] not in recent_ids]
        for row in more[:pool_target - len(recent)]:
            start = dt.datetime.fromisoformat(row["StartDate"])
            duration = dt.datetime.fromisoformat(row["EndDate"]) - start
            date = local_date(2025, start.astimezone(
                source_zone()).isoweekday(), 50)
            if date < dt.date(2025, 11, 17):
                date += dt.timedelta(weeks=4)
            start = timestamp(date, start.astimezone(
                source_zone()).hour)
            row["StartDate"], row["EndDate"] = start.isoformat(), (start + duration).isoformat()
            recent.append(row)
    assert len(recent) >= required
    at = 0
    available = collections.defaultdict(list)
    for row in recent:
        available[row["LocationId"]].append(row)
    for pattern, ids in sorted(zip(patterns, assignment_by_pattern),
                               key=lambda v: -count(v[0][6])):
        remaining = count(pattern[6])
        for aid in ids:
            data["Assignment"][aid - 10000]["LookupCodeId"] = (
                20000 + (data["Assignment"][aid - 10000]["LocationId"] - 10000)
                if pattern[0] == "0" else 10000 + int(pattern[0]))
        for aid in ids:
            if not remaining:
                break
            # Bin-pack real duties into existing pattern assignments; locations agree.
            loc = max(available, key=lambda k: (len(available[k]), -k))
            a = data["Assignment"][aid - 10000]
            a["LocationId"] = loc
            if pattern[0] == "0":
                a["LookupCodeId"] = 20000 + loc - 10000
            take = min(remaining, len(available[loc]))
            for _ in range(take):
                available[loc].pop()["AssignmentId"] = aid
            at += take
            remaining -= take
        if remaining:
            raise RuntimeError("Not enough real location-consistent assignment parents")
    zone = source_zone()
    duties = collections.defaultdict(list)
    # Give a small minority of the12–24h band a15h duration, retaining its measured12h median.
    for i, row in enumerate(data["Duty"]):
        start, end = map(dt.datetime.fromisoformat, (row["StartDate"], row["EndDate"]))
        if not row.get("ExpiryDate") and end - start == dt.timedelta(hours=12) and i % 10 == 0:
            row["EndDate"] = (start + dt.timedelta(hours=15)).isoformat()
        if row.get("ExpiryDate"):
            continue
        local = start.astimezone(zone)
        duties[row["LocationId"], local.year, local.isoweekday()].append(row)
    outside = 0
    used_parents = set()
    live_duties = [row for row in data["Duty"] if not row.get("ExpiryDate")]
    lower = dt.datetime.fromisoformat(CLOCK) - dt.timedelta(days=365)
    upper = dt.datetime.fromisoformat(CLOCK)

    def specification(row):
        begin = dt.datetime.fromisoformat(row["StartDate"])
        return (begin.astimezone(zone).isoweekday(), begin.astimezone(zone).hour,
                dt.datetime.fromisoformat(row["EndDate"]) - begin)

    def transplant(row, spec):
        old = dt.datetime.fromisoformat(row["StartDate"])
        date = old.astimezone(zone).date()
        weekday, hour, span = spec
        offset = (weekday - date.isoweekday()) % 7
        for days in (offset, offset - 7, offset + 7, offset - 14):
            candidate = timestamp(date + dt.timedelta(days=days), hour)
            if (candidate.astimezone(zone).year == date.year and
                    (lower <= candidate <= upper) == (lower <= old <= upper)):
                return candidate, candidate + span
        return None

    # Reserve scarce midnight/fringe-hour donor schedules before common-hour slots.
    # Only processing order changes; source row IDs and every measured marginal stay intact.
    ordered_slots = sorted(data["DutySlot"], key=lambda row: (
        dt.datetime.fromisoformat(row["StartDate"]).astimezone(zone).hour,
        -(dt.datetime.fromisoformat(row["EndDate"]) -
          dt.datetime.fromisoformat(row["StartDate"])).total_seconds(), row["Id"]))
    for row in ordered_slots:
        start = dt.datetime.fromisoformat(row["StartDate"])
        local = start.astimezone(zone)
        duration = dt.datetime.fromisoformat(row["EndDate"]) - start
        parents = duties[row["LocationId"], local.year, local.isoweekday()]
        match = None
        for parent in parents:
            pstart, pend = map(dt.datetime.fromisoformat, (parent["StartDate"], parent["EndDate"]))
            candidate = timestamp(pstart.astimezone(zone).date(), local.hour)
            if pstart <= candidate and candidate + duration <= pend:
                match = parent, candidate
                break
        if match:
            parent, start = match
            row["StartDate"], row["EndDate"] = start.isoformat(), (start + duration).isoformat()
        else:
            # Exchange complete schedule specifications between UNUSED real duties.
            # This preserves global hour/weekday/duration counts and each location/year,
            # plus the assignment's fixed last12months membership. Never move a used parent.
            free = [p for p in live_duties if p["Id"] not in used_parents
                    and p["LocationId"] == row["LocationId"]
                    and dt.datetime.fromisoformat(p["StartDate"]).astimezone(zone).year == local.year]
            exchange = None
            for keep_weekdays in (False, True):
                for donor in live_duties:
                    if donor["Id"] in used_parents:
                        continue
                    spec = specification(donor)
                    if not keep_weekdays and spec[0] != local.isoweekday():
                        continue
                    if spec[1] > local.hour:
                        continue
                    if spec[2] < dt.timedelta(hours=local.hour - spec[1]) + duration:
                        continue
                    for parent in free:
                        if donor is parent:
                            continue
                        parent_spec = specification(parent)
                        if keep_weekdays:
                            if parent_spec[0] != local.isoweekday():
                                continue
                            # Fallback: a unique midnight donor may have another
                            # weekday. Preserve BOTH weekdays, swap hour/duration.
                            a = transplant(parent, (parent_spec[0], spec[1], spec[2]))
                            b = transplant(donor, (spec[0], parent_spec[1], parent_spec[2]))
                        else:
                            a, b = transplant(parent, spec), transplant(donor, parent_spec)
                        if a is not None and b is not None:
                            exchange = parent, donor, a, b
                            break
                    if exchange:
                        break
                if exchange:
                    break
            if exchange:
                parent, donor, a, b = exchange
                # Remove old weekday inventory entries before reindexing exchanged schedules.
                for target, dates in ((parent, a), (donor, b)):
                    before = dt.datetime.fromisoformat(target["StartDate"]).astimezone(zone)
                    duties[target["LocationId"], before.year, before.isoweekday()].remove(target)
                    target["StartDate"], target["EndDate"] = (x.isoformat() for x in dates)
                    after = dates[0].astimezone(zone)
                    duties[target["LocationId"], after.year, after.isoweekday()].append(target)
                start = timestamp(a[0].astimezone(zone).date(), local.hour)
                row["StartDate"], row["EndDate"] = start.isoformat(), (start + duration).isoformat()
            else:
                parent = (parents or next(v for k, v in duties.items()
                                          if k[:2] == (row["LocationId"], local.year)))[0]
                outside += 1
        row["DutyId"] = parent["Id"]
        used_parents.add(parent["Id"])
    print(f"Generated slot containment unresolved={outside} (not waived)", flush=True)
    if outside:
        raise RuntimeError("Fitted profile has unresolved parent containment; not loaded or accepted")
    # Field presence/length fit is generated from invented strings. Never copy a production value.
    for cells in r["SH-07"]:
        key = cells[0]
        if key.startswith("User.") or "FirstNotice" in key or "Certification" in key:
            continue
        table, field = key.split(".")
        null, blank, short, medium, long, huge = map(count, cells[2:])
        values = [None] * null + [""] * blank + ["Synthetic"] * short + [
            "Invented synthetic aggregate profile text"] * medium + ["X" * 150] * long + ["X" * 600] * huge
        values += [None] * (len(data[table]) - len(values))
        random.Random(SEED + key).shuffle(values)
        for row, value in zip(data[table], values):
            row[field] = value
    training = data["SheriffTraining"]
    for i, row in enumerate(training):
        row["FirstNotice"] = i < 4918
        row["TrainingCertificationExpiry"] = CLOCK if i < 16807 else None
    # Match the one suppressed blank Note cell without treating its source count as exact.
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = {"seed": SEED, "clock": CLOCK,
                "source_timezone_sha256": hashlib.sha256((OUT / "Vancouver.tzif").read_bytes()).hexdigest(),
                "unresolved_slot_containment": outside,
                "aggregate_sha256": hashlib.sha256(AGGREGATE.read_bytes()).hexdigest(),
                "inventory": {k: len(v) for k, v in r.items()},
                "rows": {t: len(v) for t, v in data.items()}}
    (OUT / "generated.json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
    (OUT / "generation.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))


def load(db):
    # Refuse before any data/schema mutation, including on repeat generation.
    pg(db, """DO $$ BEGIN
      IF to_regnamespace('synthetic') IS NOT NULL OR EXISTS(SELECT 1 FROM "Shift")
        OR EXISTS(SELECT 1 FROM "Duty") OR EXISTS(SELECT 1 FROM "Assignment")
        OR (SELECT count(*) FROM "User")>1 OR EXISTS(SELECT 1 FROM "SheriffTraining")
        OR EXISTS(SELECT 1 FROM "DutySlot") OR EXISTS(SELECT 1 FROM "SheriffLeave")
        OR EXISTS(SELECT 1 FROM "SheriffAwayLocation") OR EXISTS(SELECT 1 FROM "SheriffActingRank")
        OR EXISTS(SELECT 1 FROM "SheriffStandardTraining") OR EXISTS(SELECT 1 FROM "UserRole")
      THEN RAISE EXCEPTION 'Refused: filled shape database'; END IF;
    END $$;""")
    data = json.loads((OUT / "generated.json").read_text(encoding="utf-8"))
    seed_roles = {x["Name"]: x["Id"] for x in query(db, 'SELECT "Id","Name" FROM "Role"')}
    role_rebind = {}
    for role in data["Role"]:
        old = role["Id"]
        role["Id"] = seed_roles.get(role["Name"], old)
        role_rebind[old] = role["Id"]
    for row in data["UserRole"]:
        row["RoleId"] = role_rebind[row["RoleId"]]
    columns = query(db, """SELECT table_name,column_name,data_type,is_nullable
      FROM information_schema.columns WHERE table_schema='public' ORDER BY table_name,ordinal_position""")
    by_table = collections.defaultdict(list)
    for c in columns:
        by_table[c["table_name"]].append(c)
    sql = ["BEGIN;", "SET LOCAL TIME ZONE 'UTC';",
           "CREATE SCHEMA synthetic;",
           "CREATE TABLE synthetic.profile(k text PRIMARY KEY,v text);",
           f"INSERT INTO synthetic.profile VALUES ('generator','shape-v10'),('seed','{SEED}'),('clock','{CLOCK}');"]
    # Existing seed reference/permission rows stay present. Normalize seed wall clocks only on NEW DBs.
    for table, cols in by_table.items():
        stamps = [f'"{c["column_name"]}"=TIMESTAMPTZ \'{CLOCK}\''
                  for c in cols if c["column_name"] in ("CreatedOn", "UpdatedOn")]
        if stamps:
            sql.append(f'UPDATE "{table}" SET ' + ",".join(stamps) + ";")
    # All seed User references are the fixed system GUID. Upsert its synthetic fields.
    for table in ["Region", "Location", "User", "Role", "LookupCode", "UserRole", "Assignment", *TABLES]:
        rows = data[table]
        for i, row in enumerate(rows):
            row.setdefault("Id", 10000 + i)
            for col in by_table[table]:
                name = col["column_name"]
                if col["is_nullable"] == "NO" and name not in row:
                    if name in ("CreatedOn", "UpdatedOn"):
                        row[name] = CLOCK
                    elif col["data_type"] == "boolean":
                        row[name] = False
                    elif col["data_type"] in ("integer", "double precision"):
                        row[name] = 0
                    else:
                        raise RuntimeError(f"Unmapped required source field {table}.{name}")
        sql += ["CREATE TEMP TABLE load_json(v jsonb) ON COMMIT DROP;",
                "COPY load_json(v) FROM STDIN;"]
        # COPY text must escape JSON backslashes (bytea stays on the actual source User only).
        sql += [json.dumps(row, separators=(",", ":")).replace("\\", "\\\\") for row in rows]
        sql += ["\\.", f'INSERT INTO "{table}" SELECT (jsonb_populate_record(NULL::"{table}",v)).* FROM load_json' +
                (' ON CONFLICT ("Id") DO UPDATE SET ' +
                 ",".join(f'"{c["column_name"]}"=EXCLUDED."{c["column_name"]}"'
                          for c in by_table[table] if c["column_name"] != "Id")
                 if table in ("User", "Role") else "") + ";", "DROP TABLE load_json;"]
        if table == "User":
            # Invented bytes exist ONLY in the actual source User.Photo, never artifacts or hashes.
            ids = ",".join("'" + u["Id"] + "'" for u in rows if u.get("_GeneratePhoto"))
            sql.append(f'''UPDATE "User" SET "Photo"=decode(repeat('89',640),'hex')
                           WHERE "Id" IN ({ids});''')
    sql += ["""DO $$ DECLARE r record; value bigint; BEGIN
      FOR r IN SELECT c.relname AS tbl,a.attname AS col,
        pg_get_serial_sequence(format('%I',c.relname),a.attname) AS seq
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        JOIN pg_attribute a ON a.attrelid=c.oid WHERE n.nspname='public' AND c.relkind='r'
        AND a.attnum>0 AND NOT a.attisdropped
        AND pg_get_serial_sequence(format('%I',c.relname),a.attname) IS NOT NULL LOOP
        EXECUTE format('SELECT COALESCE(max(%I),0) FROM %I',r.col,r.tbl) INTO value;
        PERFORM setval(r.seq,GREATEST(value,1),value>0);
      END LOOP; END $$;""", "COMMIT;",
            f"COMMENT ON DATABASE {db} IS 'SS SYNTHETIC SHAPE V10';",
            """DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='ss_migration_reader')
              THEN CREATE ROLE ss_migration_reader LOGIN; END IF; END $$;""",
            f"GRANT CONNECT ON DATABASE {db} TO ss_migration_reader;",
            "GRANT USAGE ON SCHEMA public,synthetic TO ss_migration_reader;",
            "GRANT SELECT ON ALL TABLES IN SCHEMA public,synthetic TO ss_migration_reader;",
            "ALTER ROLE ss_migration_reader SET default_transaction_read_only=on;",
            f"ALTER DATABASE {db} SET default_transaction_read_only=on;"]
    pg(db, "\n".join(sql))
    print("Loaded actual source rows into " + db)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["generate", "load"])
    parser.add_argument("--database", choices=DATABASES, default=DATABASES[0])
    args = parser.parse_args()
    if args.command == "generate":
        generate()
    else:
        load(args.database)


if __name__ == "__main__":
    main()
