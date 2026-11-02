using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Unified.Scheduling.Services;

internal static class SchedulingUpdateConcurrency
{
    // Serializable updates may reject an overlapping writer with 40001 before EF can
    // observe zero affected rows. Only the four version-protected updates opt in.
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> update)
    {
        try
        {
            return await update();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            throw new DbUpdateConcurrencyException("The scheduling update overlapped another transaction.", ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        )
        {
            throw new DbUpdateConcurrencyException("The scheduling update overlapped another transaction.", ex);
        }
    }
}
