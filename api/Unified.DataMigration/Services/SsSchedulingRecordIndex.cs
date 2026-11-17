using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

// Initial capture adds only component/member receipts, not scheduling roots.
// Index immutable root IDs once; callers still reload selected receipts and lock/
// verify actual target hashes and versions inside EACH component transaction.
internal sealed class SsSchedulingRecordIndex
{
    private readonly Dictionary<string, List<long>> byNode = new(StringComparer.Ordinal);

    internal SsSchedulingRecordIndex(IEnumerable<DataMigrationRecord> records)
    {
        const string seriesPrefix = "g10:assignment:";
        const string dutyPrefix = "duty:";
        foreach (var record in records.Where(x => x.Source == "SS"))
        {
            string key;
            switch (record.EntityType)
            {
                case "Assignment":
                case "Shift":
                case "Duty":
                case "DutySlot":
                    key = $"{record.EntityType}:{record.LegacyId}";
                    break;
                case "AssignmentSeries" when record.LegacyId.StartsWith(seriesPrefix, StringComparison.Ordinal):
                    key = $"Assignment:{record.LegacyId[seriesPrefix.Length..]}";
                    break;
                case "DutyEntry" when record.LegacyId.StartsWith(dutyPrefix, StringComparison.Ordinal):
                    var end = record.LegacyId.IndexOf(":segment:", dutyPrefix.Length, StringComparison.Ordinal);
                    if (end < dutyPrefix.Length)
                        continue;
                    key = $"Duty:{record.LegacyId[dutyPrefix.Length..end]}";
                    break;
                default:
                    continue;
            }
            if (!byNode.TryGetValue(key, out var ids))
                byNode[key] = ids = [];
            ids.Add(record.Id);
        }
    }

    internal long[] SelectIds(IEnumerable<(string Type, string Id)> nodes) =>
        nodes.SelectMany(x => byNode.GetValueOrDefault($"{x.Type}:{x.Id}") ?? []).Distinct().Order().ToArray();
}
