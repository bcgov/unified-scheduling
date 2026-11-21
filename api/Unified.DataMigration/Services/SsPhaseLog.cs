using System.Diagnostics;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

/// <summary>
/// One stderr line per Scheduling phase boundary, so a pod log shows the last phase reached, its size and memory
/// before a crash or OOM kill.
/// </summary>
internal sealed class SsPhaseLog(DataMigrationRun run)
{
    private readonly Stopwatch watch = new();
    private string phase = "";

    public void Start(string name)
    {
        phase = name;
        watch.Restart();
        Write("Start", null);
    }

    public void Done(long units) => Write("Done", units);

    public void Progress(long units) => Write("Progress", units);

    private void Write(string state, long? units) =>
        Console.Error.WriteLine(
            $"SsPhase Run={run.Id} Job={run.Job} DryRun={run.DryRun} Phase={phase} State={state}"
                + (units is null ? "" : $" Units={units}")
                + $" ElapsedMs={watch.ElapsedMilliseconds} ManagedMiB={GC.GetTotalMemory(false) >> 20}"
                + $" WorkingSetMiB={Environment.WorkingSet >> 20}"
        );
}
