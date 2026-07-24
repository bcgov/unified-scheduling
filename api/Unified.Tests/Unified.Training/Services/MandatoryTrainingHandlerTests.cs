using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Services;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Tests.Training.Services;

public class MandatoryTrainingHandlerTests : IAsyncLifetime
{
    private readonly string _databaseName = $"mandatory-training-handler-{Guid.NewGuid():N}";
    private static readonly Guid EnabledUserId1 = Guid.NewGuid();
    private static readonly Guid EnabledUserId2 = Guid.NewGuid();
    private static readonly Guid DisabledUserId = Guid.NewGuid();

    private UnifiedDbContext _db = null!;
    private MandatoryTrainingHandler _handler = null!;
    private TrainingEntity _training = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite($"Data Source={_databaseName};Mode=Memory;Cache=Shared")
            .Options;

        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();

        _db.Users.AddRange(
            new User
            {
                Id = EnabledUserId1,
                IdirName = "enabled-1",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Enabled",
                LastName = "One",
                Gender = Gender.Other,
            },
            new User
            {
                Id = EnabledUserId2,
                IdirName = "enabled-2",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Enabled",
                LastName = "Two",
                Gender = Gender.Other,
            },
            new User
            {
                Id = DisabledUserId,
                IdirName = "disabled-1",
                IdirId = Guid.NewGuid(),
                IsEnabled = false,
                FirstName = "Disabled",
                LastName = "User",
                Gender = Gender.Other,
            }
        );

        _training = new TrainingEntity
        {
            Id = 100,
            Code = "MAND",
            Description = "Mandatory Training",
            Mandatory = true,
            ValidityDays = 30,
            EffectiveDate = DateTimeOffset.UtcNow,
        };

        _db.Trainings.Add(_training);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _handler = new MandatoryTrainingHandler(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task HandleCreate_WhenTrainingNotMandatory_DoesNothing()
    {
        var notMandatory = new TrainingEntity
        {
            Id = _training.Id,
            Code = _training.Code,
            Description = _training.Description,
            Mandatory = false,
            ValidityDays = _training.ValidityDays,
            EffectiveDate = _training.EffectiveDate,
        };

        await _handler.HandleAsync(notMandatory, TestContext.Current.CancellationToken);

        var records = await _db
            .UserTrainings.Where(ut => ut.TrainingId == _training.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(records);
    }

    [Fact]
    public async Task HandleCreate_WhenMandatory_ProvisionsEnabledUsersWithoutActiveRecord()
    {
        _db.UserTrainings.Add(
            new UserTraining
            {
                UserId = EnabledUserId2,
                TrainingId = _training.Id,
                AwardedOn = DateTimeOffset.UtcNow.AddDays(-5),
                EndingOn = DateTimeOffset.UtcNow.AddDays(-4),
                ExpiryDate = null,
                NoticeState = UserTrainingNoticeStates.None,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var before = DateTimeOffset.UtcNow;

        await _handler.HandleAsync(_training, TestContext.Current.CancellationToken);

        var after = DateTimeOffset.UtcNow;

        var all = await _db
            .UserTrainings.Where(ut => ut.TrainingId == _training.Id)
            .OrderBy(ut => ut.UserId)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);

        var newlyCreated = Assert.Single(all, ut => ut.UserId == EnabledUserId1);
        Assert.DoesNotContain(all, ut => ut.UserId == DisabledUserId);

        Assert.Equal(UserTrainingNoticeStates.None, newlyCreated.NoticeState);
        Assert.True(newlyCreated.AwardedOn >= before && newlyCreated.AwardedOn <= after);
        Assert.True(newlyCreated.EndingOn > newlyCreated.AwardedOn);

        Assert.NotNull(newlyCreated.ExpiryDate);
        Assert.True(newlyCreated.ExpiryDate >= before.AddDays(30));
        Assert.True(newlyCreated.ExpiryDate <= after.AddDays(30));
    }

    [Fact]
    public async Task HandleUpdate_OnlyActsWhenTransitioningFromNonMandatoryToMandatory()
    {
        var previous = new TrainingEntity
        {
            Id = _training.Id,
            Code = _training.Code,
            Description = _training.Description,
            Mandatory = false,
            ValidityDays = _training.ValidityDays,
            EffectiveDate = _training.EffectiveDate,
        };

        await _handler.HandleAsync(_training, previous, TestContext.Current.CancellationToken);

        var firstPassCount = await _db.UserTrainings.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, firstPassCount);

        var alreadyMandatoryPrevious = new TrainingEntity
        {
            Id = previous.Id,
            Code = previous.Code,
            Description = previous.Description,
            Mandatory = true,
            ValidityDays = previous.ValidityDays,
            EffectiveDate = previous.EffectiveDate,
        };
        await _handler.HandleAsync(_training, alreadyMandatoryPrevious, TestContext.Current.CancellationToken);

        var secondPassCount = await _db.UserTrainings.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(firstPassCount, secondPassCount);
    }
}
