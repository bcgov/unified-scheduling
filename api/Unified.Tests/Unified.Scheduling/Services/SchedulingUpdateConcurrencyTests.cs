using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class SchedulingUpdateConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenSerializationFails_ReportsConcurrencyWithoutRetrying(bool wrapped)
    {
        var attempts = 0;
        var postgres = new PostgresException("private database detail", "ERROR", "ERROR", "40001");
        Exception failure = wrapped ? new DbUpdateException("save failed", postgres) : postgres;

        var result = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            SchedulingUpdateConcurrency.ExecuteAsync<int>(() =>
            {
                attempts++;
                return Task.FromException<int>(failure);
            })
        );

        Assert.Same(failure, result.InnerException);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDatabaseErrorIsUnrelated_PreservesTheError()
    {
        var failure = new DbUpdateException("save failed", new PostgresException("unique", "ERROR", "ERROR", "23505"));

        var result = await Assert.ThrowsAsync<DbUpdateException>(() =>
            SchedulingUpdateConcurrency.ExecuteAsync<int>(() => Task.FromException<int>(failure))
        );

        Assert.Same(failure, result);
    }
}
