using Microsoft.EntityFrameworkCore;
using Unified.Common.Contracts;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Handlers;

namespace Unified.Tests.Training.Handlers;

public sealed class AssignMandatoryTrainingOnUserCreationHandlerTests : IAsyncLifetime
{
    private readonly string _databaseName = $"assign-mandatory-training-handler-{Guid.NewGuid():N}";
    private UnifiedDbContext _db = null!;
    private AssignMandatoryTrainingOnUserCreationHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite($"Data Source={_databaseName};Mode=Memory;Cache=Shared")
            .Options;

        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();

        _handler = new AssignMandatoryTrainingOnUserCreationHandler(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task HandleAsync_Should_Assign_Global_And_ProfileMatching_Mandatory_Trainings_Only()
    {
        var now = DateTimeOffset.UtcNow;

        var carbineProfile = await SeedTrainingProfileAsync("CARBINE");
        var dtProfile = await SeedTrainingProfileAsync("DT");
        var user = await SeedUserAsync("Casey", "Carbine", trainingProfileId: carbineProfile.Id);

        var globalMandatory = await SeedTrainingAsync(700, "GLOBAL", mandatory: true);
        var matchingMandatory = await SeedTrainingAsync(
            701,
            "CARBINE-MAND",
            mandatory: true,
            mandatoryTrainingProfileIds: [carbineProfile.Id]
        );
        _ = await SeedTrainingAsync(702, "DT-MAND", mandatory: true, mandatoryTrainingProfileIds: [dtProfile.Id]);
        _ = await SeedTrainingAsync(703, "OPTIONAL", mandatory: false);

        await _handler.HandleAsync(new UserCreatedSignal(user.Id, now), TestContext.Current.CancellationToken);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var assignedTrainingIds = await _db
            .UserTrainings.AsNoTracking()
            .Where(ut => ut.UserId == user.Id)
            .Select(ut => ut.TrainingId)
            .OrderBy(id => id)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal([globalMandatory.Id, matchingMandatory.Id], assignedTrainingIds);
    }

    private async Task<User> SeedUserAsync(string firstName, string lastName, int? trainingProfileId = null)
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
            TrainingProfileId = trainingProfileId,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user;
    }

    private async Task<global::Unified.Db.Models.Training.Training> SeedTrainingAsync(
        int id,
        string code,
        bool mandatory,
        IReadOnlyCollection<int>? mandatoryTrainingProfileIds = null
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
            Description = code,
            Mandatory = mandatory,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(-1),
            Rotating = true,
            TrainingCategoryId = category.Id,
            Order = id,
        };

        _db.Trainings.Add(training);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (mandatoryTrainingProfileIds is { Count: > 0 })
        {
            _db.TrainingProfileRequirements.AddRange(
                mandatoryTrainingProfileIds.Select(profileId => new TrainingProfileRequirement
                {
                    TrainingId = training.Id,
                    TrainingProfileTypeId = profileId,
                })
            );

            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return training;
    }

    private async Task<TrainingProfileType> SeedTrainingProfileAsync(string code)
    {
        var profile = new TrainingProfileType
        {
            Code = code,
            Name = code,
            Description = code,
        };

        _db.TrainingProfileTypes.Add(profile);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return profile;
    }
}
