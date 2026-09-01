using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Unified.Tests.TestHelpers;

/// <summary>
/// Test helper: throws before an INSERT reaches the configured table so tests can verify that a
/// failed entity/audit write rolls back the other (see AuditPipelineTests). No-op unless enabled.
/// </summary>
public sealed class SimulatedDbFailureInterceptor(IOptions<SimulatedDbFailureOptions> options) : DbCommandInterceptor
{
    private readonly SimulatedDbFailureOptions _options = options.Value;

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result
    )
    {
        ThrowIfTargetInsert(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfTargetInsert(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void ThrowIfTargetInsert(DbCommand command)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.TableName))
        {
            return;
        }

        var insertFragment = $"INSERT INTO \"{_options.TableName}\"";
        if (command.CommandText.Contains(insertFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Simulated DB failure: blocked insert into \"{_options.TableName}\" for test verification."
            );
        }
    }
}
