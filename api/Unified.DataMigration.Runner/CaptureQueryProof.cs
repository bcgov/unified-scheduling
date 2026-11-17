using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Exact isolated rehearsal instrumentation. Counts query shapes, never SQL values.
internal sealed class CaptureQueryProof : DbCommandInterceptor
{
    private long fullRootReads;
    private long selectedRootReads;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CommandText.StartsWith("-- SS migration initial capture root index", StringComparison.Ordinal))
            Interlocked.Increment(ref fullRootReads);
        if (command.CommandText.StartsWith("-- SS migration capture selected roots", StringComparison.Ordinal))
            Interlocked.Increment(ref selectedRootReads);
        return ValueTask.FromResult(result);
    }

    internal void Print() =>
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    Proof = "ActualCaptureQueryShapes",
                    FullRootIndexReads = fullRootReads,
                    FreshSelectedRootReads = selectedRootReads,
                }
            )
        );
}
