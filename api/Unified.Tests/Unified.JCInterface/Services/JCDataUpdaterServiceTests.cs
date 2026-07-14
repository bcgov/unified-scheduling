using JCCommon.Clients.LocationServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.UserManagement;
using Unified.JCInterface.Options;
using Unified.JCInterface.Services;
using Unified.Tests.TestHelpers;
using Region = Unified.Db.Models.Region;

namespace Unified.Tests.JCInterface.Services;

public class JCDataUpdaterServiceTests : IAsyncLifetime
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

        _dbContext.Users.Add(
            new User
            {
                Id = User.SystemUser,
                IdirName = "system",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "System",
                LastName = "User",
                Gender = Gender.Other,
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Builds a JCDataUpdaterService with a fake HTTP handler standing in for the
    /// JC Interface Location Services API, so no real network call is made.
    /// </summary>
    private JCDataUpdaterService CreateService(
        Func<HttpRequestMessage, string> responseFactory,
        JCInterfaceOptions? jcInterfaceOptions = null
    )
    {
        var handler = new FakeHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://jc-interface.example.com/") };
        var locationClient = new LocationServicesClient(httpClient);
        var optionsMonitor = new FakeOptionsMonitor<JCInterfaceOptions>(jcInterfaceOptions ?? new JCInterfaceOptions());

        return new JCDataUpdaterService(
            _dbContext,
            locationClient,
            NullLogger<JCDataUpdaterService>.Instance,
            optionsMonitor
        );
    }

    private JCDataUpdaterService CreateService(string responseJson, JCInterfaceOptions? jcInterfaceOptions = null) =>
        CreateService(_ => responseJson, jcInterfaceOptions);

    [Fact]
    public async Task SyncRegionsAsync_WhenCalledTwice_UpsertsRegionsWithoutDuplicating()
    {
        // Arrange
        const string regionsJson = """
            [
                { "regionId": 1, "regionName": "Vancouver Island", "regionLocations": [] },
                { "regionId": 2, "regionName": "Lower Mainland", "regionLocations": [] }
            ]
            """;
        var service = CreateService(regionsJson);

        // Act
        await service.SyncRegionsAsync();
        await service.SyncRegionsAsync();

        // Assert
        var regions = await _dbContext.Regions.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, regions.Count);
        Assert.Contains(regions, r => r.JustinId == 1 && r.Name == "Vancouver Island");
        Assert.Contains(regions, r => r.JustinId == 2 && r.Name == "Lower Mainland");
    }

    [Fact]
    public async Task SyncRegionsAsync_WhenExpireRegionsEnabled_ExpiresRegionsMissingFromJcInterface()
    {
        // Arrange
        _dbContext.Regions.Add(
            new Region
            {
                JustinId = 99,
                Name = "Stale Region",
                CreatedById = User.SystemUser,
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string regionsJson = """
            [
                { "regionId": 1, "regionName": "Vancouver Island", "regionLocations": [] }
            ]
            """;
        var service = CreateService(regionsJson, new JCInterfaceOptions { ExpireRegions = true });

        // Act
        await service.SyncRegionsAsync();

        // Assert
        var staleRegion = await _dbContext.Regions.SingleAsync(
            r => r.JustinId == 99,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(staleRegion.ExpiryDate);
    }

    [Fact]
    public async Task SyncLocationsAsync_WhenCalledTwice_UpsertsLocationsAndLinksToRegionWithoutDuplicating()
    {
        // Arrange
        _dbContext.Regions.Add(
            new Region
            {
                JustinId = 1,
                Name = "Vancouver Island",
                CreatedById = User.SystemUser,
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(
            JcInterfaceLocationRoutes,
            new JCInterfaceOptions { AssociateUsersWithNoLocationToVictoria = false }
        );

        // Act
        await service.SyncLocationsAsync();
        await service.SyncLocationsAsync();

        // Assert
        var location = await _dbContext.Locations.SingleAsync(
            l => l.AgencyId == "AGENCY1",
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Victoria Law Courts", location.Name);
        Assert.Equal("100", location.JustinCode);
        var region = await _dbContext.Regions.SingleAsync(
            r => r.JustinId == 1,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(region.Id, location.RegionId);
    }

    [Fact]
    public async Task SyncCourtRoomsAsync_WhenCalledTwice_UpsertsCourtRoomsLinkedToLocationWithoutDuplicating()
    {
        // Arrange
        _dbContext.Locations.Add(
            new Location
            {
                AgencyId = "AGENCY1",
                Name = "Victoria Law Courts",
                JustinCode = "100",
                Timezone = "America/Vancouver",
                CreatedById = User.SystemUser,
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string roomsJson = """
            [
                { "codeType": "Room", "code": "101", "shortDesc": "101", "longDesc": "Room 101", "flex": "100" }
            ]
            """;
        var service = CreateService(roomsJson);

        // Act
        await service.SyncCourtRoomsAsync();
        await service.SyncCourtRoomsAsync();

        // Assert
        var location = await _dbContext.Locations.SingleAsync(
            l => l.AgencyId == "AGENCY1",
            TestContext.Current.CancellationToken
        );
        var courtRoom = await _dbContext.CourtRooms.SingleAsync(
            cr => cr.Code == "101",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(location.Id, courtRoom.LocationId);
    }

    /// <summary>
    /// Routes JC-Interface HTTP calls made by SyncLocationsAsync: the per-region
    /// location-codes lookup and the flat locations list.
    /// </summary>
    private static string JcInterfaceLocationRoutes(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path.Contains("/locations/codes"))
            return "[100]";

        if (path.EndsWith("/locations"))
            return """
                [
                    { "codeType": "Location", "code": "AGENCY1", "shortDesc": "100", "longDesc": "Victoria Law Courts", "flex": "" }
                ]
                """;

        return "[]";
    }
}
