using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Tests.TestHelpers;
using Unified.Training;
using Unified.Training.Controllers;
using Unified.Training.Models.Calendar;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Tests.Training.Controllers;

public sealed class TrainingCalendarControllerTests
{
    [Fact]
    public void GetEvents_RequiresExistingUserTrainingsViewPolicy()
    {
        var controller = typeof(TrainingCalendarController);
        var method = controller.GetMethod(nameof(TrainingCalendarController.GetEvents))!;

        Assert.NotNull(controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        Assert.Equal(
            TrainingPolicies.UserTrainingsView,
            Assert
                .Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>())
                .Policy
        );
    }

    [Theory]
    [InlineData(0, 5, null)]
    [InlineData(6, 5, null)]
    [InlineData(1, 31, null)]
    [InlineData(5, 5, -1)]
    public async Task GetEvents_InvalidRangeOrLocation_ThrowsValidationException(
        int startDay,
        int endDay,
        int? locationId
    )
    {
        await using var db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("Data Source=:memory:").Options
        );
        var controller = new TrainingCalendarController(
            new TrainingCalendarEventService(db),
            new TrainingCalendarEventsRequestValidator()
        );
        var request = new TrainingCalendarEventsRequest
        {
            StartDate = startDay == 0 ? default : new DateOnly(2026, 1, startDay),
            EndDate = new DateOnly(2026, endDay == 31 ? 2 : 1, endDay == 31 ? 1 : endDay),
            LocationId = locationId,
        };

        if (endDay == 31)
            request = request with { EndDate = new DateOnly(2027, 2, 1) };

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetEvents(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetEvents_EmptyUserId_ThrowsValidationException()
    {
        await using var db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("Data Source=:memory:").Options
        );
        var controller = new TrainingCalendarController(
            new TrainingCalendarEventService(db),
            new TrainingCalendarEventsRequestValidator()
        );
        var request = new TrainingCalendarEventsRequest
        {
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 1),
            UserIds = [Guid.Empty],
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetEvents(request, TestContext.Current.CancellationToken)
        );
    }
}
