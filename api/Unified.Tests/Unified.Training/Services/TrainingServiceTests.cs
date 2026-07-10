using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Models;
using Unified.Training.Services;

namespace Unified.Tests.Training.Services;

public class TrainingServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _db = null!;
    private TrainingService _service = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite("Data Source=:memory:;Cache=Shared;Mode=Memory;")
            .Options;

        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();

        _db.TrainingCategories.Add(new TrainingCategory { Id = 1, Name = "Mandatory" });
        _db.Users.Add(
            new User
            {
                Id = Guid.NewGuid(),
                IdirName = "u1",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "T",
                LastName = "User",
                Gender = Gender.Other,
            }
        );

        await _db.SaveChangesAsync();

        _service = new TrainingService(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Create_WhenValid_CreatesTraining()
    {
        var request = new TrainingRequest
        {
            Code = "FIREARMS",
            Description = "Firearms Qualification",
            TrainingCategoryId = 1,
            ValidityDays = 365,
        };

        var result = await _service.CreateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Id > 0);
        Assert.Equal("FIREARMS", result.Code);
    }

    [Fact]
    public async Task Delete_WhenUsedByUserTraining_ThrowsInvalidOperationException()
    {
        var created = await _service.CreateAsync(
            new TrainingRequest
            {
                Code = "FIRSTAID",
                Description = "First Aid",
                TrainingCategoryId = 1,
            },
            TestContext.Current.CancellationToken
        );

        var userId = await _db.Users.Select(u => u.Id).FirstAsync(TestContext.Current.CancellationToken);

        _db.UserTrainings.Add(
            new UserTraining
            {
                UserId = userId,
                TrainingId = created.Id,
                AwardedOn = DateTimeOffset.UtcNow,
                NoticeState = UserTrainingNoticeStates.None,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteAsync(created.Id, TestContext.Current.CancellationToken)
        );
    }
}
