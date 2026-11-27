using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

/// <summary>
/// PerRow rerun speed-up (<see cref="Options.MigrationJobOptions.PrefetchReplayRecords"/>): crosswalk records are
/// read untracked a page at a time, in source-list order, instead of one query per row.
/// <para>
/// A cached value is trusted only for a key EF has never tracked in this job. Every crosswalk write (insert,
/// update, rebind, delete) goes through a tracked entity, so an untracked key still holds the value its page read,
/// also after a savepoint rollback. A tracked, duplicated or unpaged key always takes the original per-row query.
/// </para>
/// </summary>
internal sealed class SsReplayPrefetch : IDisposable
{
    internal const int PageSize = 1000;
    internal const int PagesPerType = 16;

    internal sealed record Entry(string TargetKey, string PayloadHash, DateTimeOffset? SourceUpdatedAtUtc);

    private readonly UnifiedDbContext db;
    private readonly Dictionary<string, List<string>> ids;
    private readonly Dictionary<string, Dictionary<string, int>> positions = [];
    private readonly Dictionary<(string Type, string Id), Entry?> known = [];
    private readonly Dictionary<string, LinkedList<int>> pages = [];
    private readonly Dictionary<string, BitArray> loadedPages = [];
    private readonly HashSet<(string Type, string Id)> touched = [];

    internal long Hits { get; private set; }
    internal long Fallbacks { get; private set; }
    internal long PageLoads { get; private set; }

    internal SsReplayPrefetch(UnifiedDbContext db, IReadOnlyDictionary<string, List<SsRow>> rows)
    {
        this.db = db;
        ids = rows.ToDictionary(x => x.Key, x => x.Value.Select(r => r.Id).ToList());
        foreach (var entry in db.ChangeTracker.Entries<DataMigrationRecord>())
            Touch(entry.Entity);
        db.ChangeTracker.Tracked += OnTracked;
    }

    public void Dispose() => db.ChangeTracker.Tracked -= OnTracked;

    private void OnTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (e.Entry.Entity is DataMigrationRecord record)
            Touch(record);
    }

    private void Touch(DataMigrationRecord record)
    {
        if (record.Source == "SS")
            touched.Add((record.EntityType, record.LegacyId));
    }

    /// <summary>
    /// The crosswalk record state the per-row query would return, or <c>Known=false</c> when the caller must run
    /// that query. <paramref name="load"/> reads the key's page on a miss; otherwise only loaded pages answer.
    /// </summary>
    internal async ValueTask<(bool Known, Entry? Entry)> TryGetAsync(
        string type,
        string id,
        bool load,
        CancellationToken ct
    )
    {
        var key = (type, id);
        if (!touched.Contains(key) && !known.ContainsKey(key) && load)
            await LoadPageAsync(type, id, ct);
        if (touched.Contains(key) || !known.TryGetValue(key, out var entry))
        {
            Fallbacks++;
            return (false, null);
        }
        Hits++;
        return (true, entry);
    }

    private async Task LoadPageAsync(string type, string id, CancellationToken ct)
    {
        if (!ids.TryGetValue(type, out var list))
            return;
        if (!positions.TryGetValue(type, out var index))
        {
            index = new Dictionary<string, int>(list.Count);
            for (var i = 0; i < list.Count; i++)
                index.TryAdd(list[i], i);
            positions[type] = index;
        }
        if (!index.TryGetValue(id, out var position))
            return;
        var page = position / PageSize;
        if (!loadedPages.TryGetValue(type, out var loadedOnce))
        {
            loadedOnce = new BitArray(list.Count / PageSize + (list.Count % PageSize == 0 ? 0 : 1));
            loadedPages[type] = loadedOnce;
        }
        // A grouped write can revisit source pages in a completely different order. Never reload an
        // evicted 1000-row page: a miss falls back to the original per-row/group query instead.
        if (loadedOnce[page])
            return;
        var loaded = pages.TryGetValue(type, out var order) ? order : pages[type] = new LinkedList<int>();
        var slice = PageIds(list, page);
        var rows = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS" && x.EntityType == type && slice.Contains(x.LegacyId))
            .Select(x => new
            {
                x.LegacyId,
                x.TargetKey,
                x.PayloadHash,
                x.SourceUpdatedAtUtc,
            })
            .ToListAsync(ct);
        PageLoads++;
        foreach (var legacyId in slice)
            known[(type, legacyId)] = null;
        foreach (var group in rows.GroupBy(x => x.LegacyId))
        {
            if (group.Count() > 1)
            {
                // The per-row query fails on a duplicate crosswalk; never answer for it.
                known.Remove((type, group.Key));
                touched.Add((type, group.Key));
                continue;
            }
            var row = group.Single();
            known[(type, row.LegacyId)] = new Entry(row.TargetKey, row.PayloadHash, row.SourceUpdatedAtUtc);
        }
        loadedOnce[page] = true;
        loaded.AddFirst(page);
        if (loaded.Count > PagesPerType)
        {
            foreach (var legacyId in PageIds(list, loaded.Last!.Value))
                known.Remove((type, legacyId));
            loaded.RemoveLast();
        }
    }

    private static List<string> PageIds(List<string> list, int page)
    {
        var start = page * PageSize;
        return list.GetRange(start, Math.Min(PageSize, list.Count - start)).Distinct().ToList();
    }
}
