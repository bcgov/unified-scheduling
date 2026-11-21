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

    /// <summary>The phase's work was proven unnecessary and not performed.</summary>
    public void Skipped() => Write("Skipped", null);

    public void Progress(long units) => Write("Progress", units);

    /// <summary>Test hook: receives (run id, phase, state, units) for every line.</summary>
    internal static Action<Guid, string, string, long?>? Observer { get; set; }

    private void Write(string state, long? units)
    {
        Observer?.Invoke(run.Id, phase, state, units);
        Console.Error.WriteLine(
            $"SsPhase Run={run.Id} Job={run.Job} DryRun={run.DryRun} Phase={phase} State={state}"
                + (units is null ? "" : $" Units={units}")
                + $" ElapsedMs={watch.ElapsedMilliseconds} ManagedMiB={GC.GetTotalMemory(false) >> 20}"
                + $" WorkingSetMiB={Environment.WorkingSet >> 20}"
        );
    }
}
