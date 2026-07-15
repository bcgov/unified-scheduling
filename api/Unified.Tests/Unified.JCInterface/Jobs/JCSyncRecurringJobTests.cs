using JCCommon.Clients.LocationServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.JCInterface.Jobs;
using Unified.JCInterface.Options;
using Unified.JCInterface.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.JCInterface.Jobs;

public class JCSyncRecurringJobTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Builds a JCSyncRecurringJob backed by a real JCDataUpdaterService, with a fake HTTP
    /// handler standing in for the JC Interface Location Services API so no real network
    /// call is made.
    /// </summary>
    private JCSyncRecurringJob CreateJob(JCInterfaceOptions? jcInterfaceOptions = null)
    {
        var handler = new FakeHttpMessageHandler(_ => "[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://jc-interface.example.com/") };
        var locationClient = new LocationServicesClient(httpClient);
        var optionsMonitor = new FakeOptionsMonitor<JCInterfaceOptions>(jcInterfaceOptions ?? new JCInterfaceOptions());

        var jcDataUpdaterService = new JCDataUpdaterService(
            _dbContext,
            locationClient,
            NullLogger<JCDataUpdaterService>.Instance,
            optionsMonitor
        );

        return new JCSyncRecurringJob(jcDataUpdaterService, optionsMonitor, NullLogger<JCSyncRecurringJob>.Instance);
    }

    [Fact]
    public void JobName_IsStableAcrossInstances()
    {
        // Arrange
        var job = CreateJob();

        // Act & Assert
        Assert.Equal(nameof(JCSyncRecurringJob), job.JobName);
    }
}
