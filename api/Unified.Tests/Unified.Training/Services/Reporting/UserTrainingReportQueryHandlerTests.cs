using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Services.Reporting;

namespace Unified.Tests.Training.Services.Reporting;

public class UserTrainingReportQueryHandlerTests : IAsyncLifetime
{
    private readonly string _databaseName = $"user-training-report-query-handler-{Guid.NewGuid():N}";
    private UnifiedDbContext _db = null!;
    private UserTrainingReportQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite($"Data Source={_databaseName};Mode=Memory;Cache=Shared")
            .Options;

        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();

        _handler = new UserTrainingReportQueryHandler(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Sort_By_UserDisplayName_Descending()
    {
        var training = await SeedTrainingAsync(100, "TRN", "Training", mandatory: false);

        var adams = await SeedUserAsync("Bob", "Adams");
        var brown = await SeedUserAsync("Carl", "Brown");
        var zed = await SeedUserAsync("Amy", "Zed");

        await SeedUserTrainingAsync(adams.Id, training.Id, awardedOn: DateTimeOffset.UtcNow.AddDays(-3));
        await SeedUserTrainingAsync(brown.Id, training.Id, awardedOn: DateTimeOffset.UtcNow.AddDays(-2));
        await SeedUserTrainingAsync(zed.Id, training.Id, awardedOn: DateTimeOffset.UtcNow.AddDays(-1));

        var filters = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = ["active"],
        };

        var result = (UserTrainingReportResponse)
            await _handler.ExecuteAsync(
                filters,
                page: 1,
                pageSize: 10,
                sortBy: "userDisplayName",
                sortDirection: "desc",
                timeZone: null,
                cancellationToken: TestContext.Current.CancellationToken
            );

        var rows = result.Rows.ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Zed, Amy", rows[0].UserDisplayName);
        Assert.Equal("Brown, Carl", rows[1].UserDisplayName);
        Assert.Equal("Adams, Bob", rows[2].UserDisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Apply_Default_Sort_When_SortBy_Is_Unknown()
    {
        var training = await SeedTrainingAsync(200, "TRN", "Training", mandatory: false);

        var brown = await SeedUserAsync("Charlie", "Brown");
        var adams = await SeedUserAsync("Alice", "Adams");

        await SeedUserTrainingAsync(brown.Id, training.Id, awardedOn: DateTimeOffset.UtcNow.AddDays(-1));
        await SeedUserTrainingAsync(adams.Id, training.Id, awardedOn: DateTimeOffset.UtcNow.AddDays(-2));

        var result = (UserTrainingReportResponse)
            await _handler.ExecuteAsync(
                filters: new Dictionary<string, IReadOnlyCollection<string>>(),
                page: 1,
                pageSize: 10,
                sortBy: "notARealColumn",
                sortDirection: null,
                timeZone: null,
                cancellationToken: TestContext.Current.CancellationToken
            );

        var rows = result.Rows.ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Adams, Alice", rows[0].UserDisplayName);
        Assert.Equal("Brown, Charlie", rows[1].UserDisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_Status_Filter_Is_Invalid()
    {
        var filters = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = ["sideways"],
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(
                filters,
                page: 1,
                pageSize: 10,
                sortBy: null,
                sortDirection: null,
                timeZone: null,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("status", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_StartDate_Is_After_EndDate()
    {
        var filters = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["startDate"] = ["2026-09-10"],
            ["endDate"] = ["2026-09-01"],
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(
                filters,
                page: 1,
                pageSize: 10,
                sortBy: null,
                sortDirection: null,
                timeZone: null,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("startDate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Include_Missing_Mandatory_And_Mark_Status_As_NotTaken()
    {
        var mandatory = await SeedTrainingAsync(300, "MAND", "Mandatory", mandatory: true);

        var assignedUser = await SeedUserAsync("Alex", "Assigned");
        var missingUser = await SeedUserAsync("Mia", "Missing");

        await SeedUserTrainingAsync(assignedUser.Id, mandatory.Id, awardedOn: DateTimeOffset.UtcNow.AddDays(-2));

        var result = (UserTrainingReportResponse)
            await _handler.ExecuteAsync(
                filters: new Dictionary<string, IReadOnlyCollection<string>>(),
                page: 1,
                pageSize: 10,
                sortBy: "userDisplayName",
                sortDirection: "asc",
                timeZone: null,
                cancellationToken: TestContext.Current.CancellationToken
            );

        Assert.Equal(2, result.TotalRows);

        var missingRow = Assert.Single(result.Rows, row => row.HasMissingMandatoryTrainingAssignment);
        Assert.Equal("Not Taken", missingRow.Status);
        Assert.Equal("Missing, Mia", missingRow.UserDisplayName);

        var activeRow = Assert.Single(result.Rows, row => !row.HasMissingMandatoryTrainingAssignment);
        Assert.Equal("Active", activeRow.Status);
        Assert.Equal("Assigned, Alex", activeRow.UserDisplayName);
    }

    private async Task<User> SeedUserAsync(string firstName, string lastName)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = $"{firstName}.{lastName}.{Guid.NewGuid():N}",
            IdirId = Guid.NewGuid(),
            IsEnabled = true,
            FirstName = firstName,
            LastName = lastName,
            Gender = Gender.Other,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user;
    }

    private async Task<global::Unified.Db.Models.Training.Training> SeedTrainingAsync(
        int id,
        string code,
        string description,
        bool mandatory
    )
    {
        var category = await _db.TrainingCategories.FirstOrDefaultAsync(
            c => c.Id == 1,
            TestContext.Current.CancellationToken
        );
        if (category is null)
        {
            category = new TrainingCategory { Id = 1, Name = "Category" };
            _db.TrainingCategories.Add(category);
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var training = new global::Unified.Db.Models.Training.Training
        {
            Id = id,
            Code = code,
            Description = description,
            Mandatory = mandatory,
            Rotating = true,
            TrainingCategoryId = category.Id,
        };

        _db.Trainings.Add(training);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return training;
    }

    private async Task SeedUserTrainingAsync(Guid userId, int trainingId, DateTimeOffset awardedOn)
    {
        _db.UserTrainings.Add(
            new UserTraining
            {
                UserId = userId,
                TrainingId = trainingId,
                Version = 1,
                AwardedOn = awardedOn,
                EndingOn = awardedOn.AddHours(1),
                ExpiryDate = awardedOn.AddDays(90),
                NoticeState = UserTrainingNoticeStates.None,
            }
        );

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
