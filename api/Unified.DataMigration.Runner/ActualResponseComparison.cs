using System.Text.Json.Nodes;

// Compares executed response values, never generates SS statuses/rows/OT results.
internal static class ActualResponseComparison
{
    internal static string Canonical(string text)
    {
        JsonNode? Sort(JsonNode? node) =>
            node switch
            {
                JsonObject obj => new JsonObject(
                    obj.OrderBy(x => x.Key, StringComparer.Ordinal)
                        .Select(x => KeyValuePair.Create(x.Key, Sort(x.Value)))
                        .ToArray()
                ),
                JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
                _ => node?.DeepClone(),
            };
        return Sort(JsonNode.Parse(text))!.ToJsonString();
    }

    internal static object[] BySurface(
        IReadOnlyList<NormalizedTargetExport.Row> source,
        IReadOnlyList<NormalizedTargetExport.Row> target
    )
    {
        string Identity(NormalizedTargetExport.Row row) => Canonical(row.Key);
        return source
            .Concat(target)
            .Select(x => (x.Window, x.Report))
            .Distinct()
            .OrderBy(x => x.Window)
            .ThenBy(x => x.Report)
            .Select(surface =>
            {
                var s = source.Where(x => (x.Window, x.Report) == surface).ToList();
                var t = target.Where(x => (x.Window, x.Report) == surface).ToList();
                var sg = s.GroupBy(Identity).ToDictionary(x => x.Key, x => x.ToList());
                var tg = t.GroupBy(Identity).ToDictionary(x => x.Key, x => x.ToList());
                var pairs = sg.Where(x =>
                        x.Value.Count == 1 && tg.TryGetValue(x.Key, out var matches) && matches.Count == 1
                    )
                    .Select(x =>
                        (
                            Source: JsonNode.Parse(x.Value[0].Value)!.AsObject(),
                            Target: JsonNode.Parse(tg[x.Key][0].Value)!.AsObject()
                        )
                    )
                    .ToList();
                var fields = pairs
                    .SelectMany(x => x.Source.Select(y => y.Key).Concat(x.Target.Select(y => y.Key)))
                    .Distinct()
                    .OrderBy(x => x, StringComparer.Ordinal);
                var sc = s.GroupBy(x => Identity(x) + "\n" + Canonical(x.Value))
                    .ToDictionary(x => x.Key, x => x.Count());
                var tc = t.GroupBy(x => Identity(x) + "\n" + Canonical(x.Value))
                    .ToDictionary(x => x.Key, x => x.Count());
                var exact = sc.Sum(x => Math.Min(x.Value, tc.GetValueOrDefault(x.Key)));
                return (object)
                    new
                    {
                        surface.Window,
                        surface.Report,
                        SourceRows = s.Count,
                        TargetRows = t.Count,
                        ExactMatchedRows = exact,
                        MissingRows = s.Count - exact,
                        UnexpectedRows = t.Count - exact,
                        MissingIdentityRows = sg.Where(x => !tg.ContainsKey(x.Key)).Sum(x => x.Value.Count),
                        UnexpectedIdentityRows = tg.Where(x => !sg.ContainsKey(x.Key)).Sum(x => x.Value.Count),
                        UniquelyPairedRows = pairs.Count,
                        AmbiguousIdentityGroups = sg
                            .Keys.Concat(tg.Keys)
                            .Distinct()
                            .Count(key => sg.GetValueOrDefault(key)?.Count > 1 || tg.GetValueOrDefault(key)?.Count > 1),
                        Fields = fields
                            .Select(field => new
                            {
                                Field = field,
                                ComparedRows = pairs.Count,
                                EqualRows = pairs.Count(p =>
                                    p.Source.ContainsKey(field)
                                    && p.Target.ContainsKey(field)
                                    && JsonNode.DeepEquals(p.Source[field], p.Target[field])
                                ),
                                DifferentRows = pairs.Count(p =>
                                    !p.Source.ContainsKey(field)
                                    || !p.Target.ContainsKey(field)
                                    || !JsonNode.DeepEquals(p.Source[field], p.Target[field])
                                ),
                                SourceValueTargetNull = pairs.Count(p =>
                                    p.Source[field] is not null && p.Target[field] is null
                                ),
                                SourceNullTargetValue = pairs.Count(p =>
                                    p.Source[field] is null && p.Target[field] is not null
                                ),
                            })
                            .ToArray(),
                    };
            })
            .ToArray();
    }

    // Representation-only evidence, separate from strict row parity. Never expands
    // one actual target row into multiple invented source-ID rows.
    internal static object[] ShiftGroupCoverage(
        IReadOnlyList<NormalizedTargetExport.Row> source,
        IReadOnlyList<NormalizedTargetExport.Row> target,
        IReadOnlyList<Unified.Db.Models.DataMigration.DataMigrationRecord> links
    )
    {
        var result = new List<object>();
        foreach (
            var group in links.Where(x => x.EntityType == "Shift").GroupBy(x => x.TargetKey).Where(x => x.Count() > 1)
        )
        {
            var ids = group.Select(x => int.Parse(x.LegacyId)).OrderBy(x => x).ToArray();
            var representative = group.OrderBy(x => x.LegacyId, StringComparer.Ordinal).First().LegacyId;
            foreach (var window in source.Concat(target).Select(x => x.Window).Distinct().OrderBy(x => x))
            {
                var s = source
                    .Where(x =>
                        x.Report == "shifts"
                        && x.Window == window
                        && ids.Contains(JsonNode.Parse(x.Key)!["shiftId"]!.GetValue<int>())
                    )
                    .ToList();
                var t = target
                    .Where(x =>
                        x.Report == "shifts"
                        && x.Window == window
                        && JsonNode.Parse(x.Key)!["shiftId"]!.ToString() == representative
                    )
                    .ToList();
                if (s.Count == 0 && t.Count == 0)
                    continue;
                (DateTimeOffset Start, DateTimeOffset End)[] Union(IEnumerable<NormalizedTargetExport.Row> rows)
                {
                    var merged = new List<(DateTimeOffset Start, DateTimeOffset End)>();
                    foreach (
                        var row in rows.Select(x => JsonNode.Parse(x.Value)!)
                            .Select(x =>
                                (
                                    Start: DateTimeOffset.Parse(x["start"]!.GetValue<string>()).ToUniversalTime(),
                                    End: DateTimeOffset.Parse(x["end"]!.GetValue<string>()).ToUniversalTime()
                                )
                            )
                            .OrderBy(x => x.Start)
                            .ThenBy(x => x.End)
                    )
                    {
                        if (merged.Count > 0 && row.Start <= merged[^1].End)
                            merged[^1] = (merged[^1].Start, row.End > merged[^1].End ? row.End : merged[^1].End);
                        else
                            merged.Add(row);
                    }
                    return merged.ToArray();
                }
                var su = Union(s);
                var tu = Union(t);
                string[] Attributes(IEnumerable<NormalizedTargetExport.Row> rows) =>
                    rows.Select(x =>
                        {
                            var key = JsonNode.Parse(x.Key)!;
                            var value = JsonNode.Parse(x.Value)!;
                            return $"{key["locationId"]}|{value["sheriffId"]}|{value["timezone"]}";
                        })
                        .Distinct()
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToArray();
                result.Add(
                    new
                    {
                        Window = window,
                        SourceShiftIds = ids,
                        SourceRows = s.Count,
                        TargetRows = t.Count,
                        AllMembersReturned = s.Select(x => JsonNode.Parse(x.Key)!["shiftId"]!.GetValue<int>())
                            .OrderBy(x => x)
                            .SequenceEqual(ids),
                        SameLocationUserTimezone = Attributes(s).SequenceEqual(Attributes(t)),
                        IntervalUnionEqual = su.SequenceEqual(tu),
                        SourceIntervalUnion = su.Select(x => new { x.Start, x.End }).ToArray(),
                        TargetIntervalUnion = tu.Select(x => new { x.Start, x.End }).ToArray(),
                        StrictRowsExpanded = false,
                    }
                );
            }
        }
        return result.ToArray();
    }
}
