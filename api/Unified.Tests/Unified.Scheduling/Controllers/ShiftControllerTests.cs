using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization.Claims;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Controllers;

public class ShiftControllerTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetShiftSeries_WhenCalled_ReturnsOkWithServiceResult()
    {
        // Arrange
        var queryParams = new ShiftSeriesQueryParams { EventSeriesId = 10, UserId = UserA };
        var expected = new[] { CreateShiftSeriesResponse(1, 10) };
        var service = new FakeShiftService { ShiftSeriesResults = expected };
        var controller = CreateShiftController(service);

        // Act
        var result = await controller.GetShiftSeries(queryParams, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
        Assert.Same(queryParams, service.LastShiftSeriesQueryParams);
    }

    [Fact]
    public async Task GetShiftSeriesById_WhenFound_ReturnsOk()
    {
        // Arrange
        var expected = CreateShiftSeriesResponse(1, 10);
        var controller = CreateShiftController(new FakeShiftService { ShiftSeriesResult = expected });

        // Act
        var result = await controller.GetShiftSeriesById(1, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public async Task GetShiftSeriesById_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.GetShiftSeriesById(1, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateShiftSeries_WhenValid_ReturnsCreated()
    {
        // Arrange
        var request = CreateShiftSeriesRequest();
        var created = CreateShiftSeriesResponse(7, 20);
        var service = new FakeShiftService { CreatedShiftSeries = created };
        var controller = CreateShiftController(service);

        // Act
        var result = await controller.CreateShiftSeries(request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/shifts/series/7", createdResult.Location);
        Assert.Same(created, createdResult.Value);
        Assert.Same(request, service.LastShiftSeriesRequest);
    }

    [Fact]
    public async Task CreateShiftSeries_WhenInvalid_ThrowsValidationException()
    {
        // Arrange
        var request = CreateShiftSeriesRequest(userIds: []);
        var controller = CreateShiftController(new FakeShiftService());

        // Act / Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.CreateShiftSeries(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateShiftSeries_WhenFound_ReturnsOk()
    {
        // Arrange
        var expected = CreateShiftSeriesResponse(7, 20);
        var service = new FakeShiftService { UpdatedShiftSeries = expected };
        var controller = CreateShiftController(service);

        // Act
        var result = await controller.UpdateShiftSeries(
            7,
            CreateShiftSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public async Task UpdateShiftSeries_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.UpdateShiftSeries(
            7,
            CreateShiftSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task PublishShiftSeries_WhenFound_ReturnsOk()
    {
        // Arrange
        var expected = CreateShiftSeriesResponse(7, 20);
        var controller = CreateShiftController(new FakeShiftService { PublishedShiftSeries = expected });

        // Act
        var result = await controller.PublishShiftSeries(7, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public async Task PublishShiftSeries_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.PublishShiftSeries(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ExpireShiftSeries_WhenUserClaimMissing_ReturnsForbid()
    {
        // Arrange
        var service = new FakeShiftService { ExpiredShiftSeries = CreateShiftSeriesResponse(7, 20) };
        var controller = CreateShiftController(service, userId: null);

        // Act
        var result = await controller.ExpireShiftSeries(7, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        Assert.Null(service.LastCancelledByUserId);
    }

    [Fact]
    public async Task ExpireShiftSeries_WhenFound_ReturnsOkAndPassesCurrentUser()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var expected = CreateShiftSeriesResponse(7, 20);
        var service = new FakeShiftService { ExpiredShiftSeries = expected };
        var controller = CreateShiftController(service, currentUserId);
        var request = new ExpireShiftRequest { CancellationReason = "Done" };

        // Act
        var result = await controller.ExpireShiftSeries(7, request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
        Assert.Same(request, service.LastExpireShiftRequest);
        Assert.Equal(currentUserId, service.LastCancelledByUserId);
    }

    [Fact]
    public async Task ExpireShiftSeries_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService(), Guid.NewGuid());

        // Act
        var result = await controller.ExpireShiftSeries(7, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteShiftSeries_WhenDeleted_ReturnsNoContent()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService { DeleteShiftSeriesResult = true });

        // Act
        var result = await controller.DeleteShiftSeries(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteShiftSeries_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.DeleteShiftSeries(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetShiftEntries_WhenCalled_ReturnsOkWithServiceResult()
    {
        // Arrange
        var queryParams = new ShiftEntryQueryParams
        {
            ShiftSeriesId = 1,
            EventId = 2,
            UserId = UserA,
        };
        var expected = new[] { CreateShiftEntryResponse(2, 1, 30) };
        var service = new FakeShiftService { ShiftEntryResults = expected };
        var controller = CreateShiftController(service);

        // Act
        var result = await controller.GetShiftEntries(queryParams, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
        Assert.Same(queryParams, service.LastShiftEntryQueryParams);
    }

    [Fact]
    public async Task GetShiftEntryById_WhenFound_ReturnsOk()
    {
        // Arrange
        var expected = CreateShiftEntryResponse(2, 1, 30);
        var controller = CreateShiftController(new FakeShiftService { ShiftEntryResult = expected });

        // Act
        var result = await controller.GetShiftEntryById(2, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public async Task GetShiftEntryById_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.GetShiftEntryById(2, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateShiftEntry_WhenValid_ReturnsCreated()
    {
        // Arrange
        var request = CreateShiftEntryRequest();
        var created = CreateShiftEntryResponse(8, 1, 30);
        var service = new FakeShiftService { CreatedShiftEntry = created };
        var controller = CreateShiftController(service);

        // Act
        var result = await controller.CreateShiftEntry(request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/shifts/entries/8", createdResult.Location);
        Assert.Same(created, createdResult.Value);
        Assert.Same(request, service.LastShiftEntryRequest);
    }

    [Fact]
    public async Task CreateShiftEntry_WhenInvalid_ThrowsValidationException()
    {
        // Arrange
        var request = CreateShiftEntryRequest(userIds: [UserA, UserA]);
        var controller = CreateShiftController(new FakeShiftService());

        // Act / Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.CreateShiftEntry(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateShiftEntry_WhenFound_ReturnsOk()
    {
        // Arrange
        var expected = CreateShiftEntryResponse(8, 1, 30);
        var controller = CreateShiftController(new FakeShiftService { UpdatedShiftEntry = expected });

        // Act
        var result = await controller.UpdateShiftEntry(
            8,
            CreateShiftEntryRequest(),
            TestContext.Current.CancellationToken
        );

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public async Task UpdateShiftEntry_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.UpdateShiftEntry(
            8,
            CreateShiftEntryRequest(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task PublishShiftEntry_WhenFound_ReturnsOk()
    {
        // Arrange
        var expected = CreateShiftEntryResponse(8, 1, 30);
        var controller = CreateShiftController(new FakeShiftService { PublishedShiftEntry = expected });

        // Act
        var result = await controller.PublishShiftEntry(8, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public async Task PublishShiftEntry_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.PublishShiftEntry(8, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ExpireShiftEntry_WhenUserClaimMissing_ReturnsForbid()
    {
        // Arrange
        var service = new FakeShiftService { ExpiredShiftEntry = CreateShiftEntryResponse(8, 1, 30) };
        var controller = CreateShiftController(service, userId: null);

        // Act
        var result = await controller.ExpireShiftEntry(8, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        Assert.Null(service.LastCancelledByUserId);
    }

    [Fact]
    public async Task ExpireShiftEntry_WhenFound_ReturnsOkAndPassesDefaultRequest()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var expected = CreateShiftEntryResponse(8, 1, 30);
        var service = new FakeShiftService { ExpiredShiftEntry = expected };
        var controller = CreateShiftController(service, currentUserId);

        // Act
        var result = await controller.ExpireShiftEntry(8, null, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
        Assert.NotNull(service.LastExpireShiftRequest);
        Assert.Equal(currentUserId, service.LastCancelledByUserId);
    }

    [Fact]
    public async Task ExpireShiftEntry_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService(), Guid.NewGuid());

        // Act
        var result = await controller.ExpireShiftEntry(8, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteShiftEntry_WhenDeleted_ReturnsNoContent()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService { DeleteShiftEntryResult = true });

        // Act
        var result = await controller.DeleteShiftEntry(8, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteShiftEntry_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateShiftController(new FakeShiftService());

        // Act
        var result = await controller.DeleteShiftEntry(8, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    private static ShiftController CreateShiftController(FakeShiftService service, Guid? userId = null)
    {
        var controller = new ShiftController(
            service,
            new ShiftSeriesRequestValidator(),
            new ShiftEntryRequestValidator()
        );

        var claims = userId.HasValue ? new[] { new Claim(UnifiedClaimTypes.UserId, userId.Value.ToString()) } : [];
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) },
        };

        return controller;
    }

    private static ShiftSeriesRequest CreateShiftSeriesRequest(IReadOnlyCollection<Guid>? userIds = null) =>
        new()
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            UserIds = userIds ?? [UserA, UserB],
        };

    private static ShiftEntryRequest CreateShiftEntryRequest(IReadOnlyCollection<Guid>? userIds = null) =>
        new()
        {
            ShiftSeriesId = 1,
            Title = "Entry",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            UserIds = userIds ?? [UserA, UserB],
        };

    private static ShiftSeriesResponse CreateShiftSeriesResponse(int id, int eventSeriesId) =>
        new()
        {
            Id = id,
            EventSeriesId = eventSeriesId,
            UserIds = [UserA, UserB],
        };

    private static ShiftEntryResponse CreateShiftEntryResponse(int id, int? shiftSeriesId, int eventId) =>
        new()
        {
            Id = id,
            ShiftSeriesId = shiftSeriesId,
            EventId = eventId,
            UserIds = [UserA, UserB],
        };

    private sealed class FakeShiftService : IShiftService
    {
        public IReadOnlyCollection<ShiftSeriesResponse> ShiftSeriesResults { get; init; } = [];
        public ShiftSeriesResponse? ShiftSeriesResult { get; init; }
        public ShiftSeriesResponse? CreatedShiftSeries { get; init; }
        public ShiftSeriesResponse? UpdatedShiftSeries { get; init; }
        public ShiftSeriesResponse? PublishedShiftSeries { get; init; }
        public ShiftSeriesResponse? ExpiredShiftSeries { get; init; }
        public bool DeleteShiftSeriesResult { get; init; }
        public IReadOnlyCollection<ShiftEntryResponse> ShiftEntryResults { get; init; } = [];
        public ShiftEntryResponse? ShiftEntryResult { get; init; }
        public ShiftEntryResponse? CreatedShiftEntry { get; init; }
        public ShiftEntryResponse? UpdatedShiftEntry { get; init; }
        public ShiftEntryResponse? PublishedShiftEntry { get; init; }
        public ShiftEntryResponse? ExpiredShiftEntry { get; init; }
        public bool DeleteShiftEntryResult { get; init; }
        public ShiftSeriesQueryParams? LastShiftSeriesQueryParams { get; private set; }
        public ShiftSeriesRequest? LastShiftSeriesRequest { get; private set; }
        public ShiftEntryQueryParams? LastShiftEntryQueryParams { get; private set; }
        public ShiftEntryRequest? LastShiftEntryRequest { get; private set; }
        public ExpireShiftRequest? LastExpireShiftRequest { get; private set; }
        public Guid? LastCancelledByUserId { get; private set; }

        public Task<IReadOnlyCollection<ShiftSeriesResponse>> GetShiftSeriesAsync(
            ShiftSeriesQueryParams? queryParams = null,
            CancellationToken cancellationToken = default
        )
        {
            LastShiftSeriesQueryParams = queryParams;
            return Task.FromResult(ShiftSeriesResults);
        }

        public Task<ShiftSeriesResponse?> GetShiftSeriesByIdAsync(
            int id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(ShiftSeriesResult);

        public Task<ShiftSeriesResponse> CreateShiftSeriesAsync(
            ShiftSeriesRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastShiftSeriesRequest = request;
            return Task.FromResult(CreatedShiftSeries!);
        }

        public Task<ShiftSeriesResponse?> UpdateShiftSeriesAsync(
            int id,
            ShiftSeriesRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastShiftSeriesRequest = request;
            return Task.FromResult(UpdatedShiftSeries);
        }

        public Task<ShiftSeriesResponse?> PublishShiftSeriesAsync(
            int id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(PublishedShiftSeries);

        public Task<ShiftSeriesResponse?> ExpireShiftSeriesAsync(
            int id,
            ExpireShiftRequest request,
            Guid? cancelledByUserId = null,
            CancellationToken cancellationToken = default
        )
        {
            LastExpireShiftRequest = request;
            LastCancelledByUserId = cancelledByUserId;
            return Task.FromResult(ExpiredShiftSeries);
        }

        public Task<bool> DeleteShiftSeriesAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteShiftSeriesResult);

        public Task<IReadOnlyCollection<ShiftEntryResponse>> GetShiftEntriesAsync(
            ShiftEntryQueryParams? queryParams = null,
            CancellationToken cancellationToken = default
        )
        {
            LastShiftEntryQueryParams = queryParams;
            return Task.FromResult(ShiftEntryResults);
        }

        public Task<ShiftEntryResponse?> GetShiftEntryByIdAsync(
            int id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(ShiftEntryResult);

        public Task<ShiftEntryResponse> CreateShiftEntryAsync(
            ShiftEntryRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastShiftEntryRequest = request;
            return Task.FromResult(CreatedShiftEntry!);
        }

        public Task<ShiftEntryResponse?> UpdateShiftEntryAsync(
            int id,
            ShiftEntryRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastShiftEntryRequest = request;
            return Task.FromResult(UpdatedShiftEntry);
        }

        public Task<ShiftEntryResponse?> PublishShiftEntryAsync(
            int id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(PublishedShiftEntry);

        public Task<ShiftEntryResponse?> ExpireShiftEntryAsync(
            int id,
            ExpireShiftRequest request,
            Guid? cancelledByUserId = null,
            CancellationToken cancellationToken = default
        )
        {
            LastExpireShiftRequest = request;
            LastCancelledByUserId = cancelledByUserId;
            return Task.FromResult(ExpiredShiftEntry);
        }

        public Task<bool> DeleteShiftEntryAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteShiftEntryResult);
    }
}
