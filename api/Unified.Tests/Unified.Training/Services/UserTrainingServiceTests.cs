using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Infrastructure.ErrorHandling;
using Unified.Tests.TestHelpers;
using Unified.Training.Models;
using Unified.Training.Services;

namespace Unified.Tests.Training.Services;

public class UserTrainingServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"user-training-service-{Guid.NewGuid():N}";
    private UnifiedDbContext _db = null!;
    private IHttpContextAccessor _httpContextAccessor = null!;
    private UserTrainingService _service = null!;

    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private const int TrainingId = 1;
    private const int TrainingWithValidityId = 2;
    private static readonly DateTimeOffset Today = DateTimeOffset.UtcNow.Date;
    private static readonly DateTimeOffset Yesterday = Today.AddDays(-1);
    private static readonly DateTimeOffset Tomorrow = Today.AddDays(1);

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite($"Data Source={_databaseName};Mode=Memory;Cache=Shared")
            .Options;

        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();

        _httpContextAccessor = new HttpContextAccessor();
        SetCallerPermissions();
        _service = new UserTrainingService(_db, _httpContextAccessor);

        await SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        _db.Users.Add(
            new User
            {
                Id = CallerId,
                IdirName = "caller",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Caller",
                LastName = "User",
                Gender = Gender.Other,
            }
        );

        _db.Users.Add(
            new User
            {
                Id = OtherUserId,
                IdirName = "other",
                IdirId = Guid.NewGuid(),
                IsEnabled = true,
                FirstName = "Other",
                LastName = "User",
                Gender = Gender.Other,
            }
        );

        var category = new TrainingCategory { Id = 1, Name = "Safety" };
        _db.TrainingCategories.Add(category);

        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = TrainingId,
                Code = "FA",
                Description = "First Aid",
                TrainingCategoryId = 1,
            }
        );

        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = TrainingWithValidityId,
                Code = "CPR",
                Description = "CPR",
                TrainingCategoryId = 1,
                ValidityDays = 365,
            }
        );

        await _db.SaveChangesAsync();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenCallerOwnsRecords_ReturnsNewestFirst()
    {
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-10), expiryDate: null);
        await SeedUserTrainingAsync(CallerId, TrainingWithValidityId, awardedOn: Today, expiryDate: Today.AddDays(365));

        var result = await _service.GetAllAsync(CallerId, CallerId, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(TrainingWithValidityId, result.First().TrainingId);
        Assert.Equal(TrainingId, result.Last().TrainingId);
    }

    [Fact]
    public async Task GetAll_WhenViewingOtherUserWithoutPermission_ThrowsForbidden()
    {
        await SeedUserTrainingAsync(OtherUserId, TrainingId, awardedOn: Today, expiryDate: null);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetAllAsync(OtherUserId, CallerId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Create_WhenValid_PersistsAndReturnsResponse()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Id > 0);
        Assert.Equal(CallerId, result.UserId);
        Assert.Equal(TrainingId, result.TrainingId);
        Assert.Equal("FA", result.TrainingCode);
        Assert.Equal("Safety", result.TrainingCategoryName);
        Assert.Null(result.ExpiryDate);
    }

    [Fact]
    public async Task Create_WhenTrainingHasValidityDays_AutoCalculatesExpiryDate()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingWithValidityId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result.ExpiryDate);
        Assert.Equal(Today.AddDays(365), result.ExpiryDate!.Value);
    }

    [Fact]
    public async Task Create_WhenExpiryDateProvided_UsesProvidedExpiryDate()
    {
        var manualExpiry = Today.AddDays(180);
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingWithValidityId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
            ExpiryDate = manualExpiry,
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: true,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(manualExpiry, result.ExpiryDate);
    }

    [Fact]
    public async Task Create_WhenExpiryDateProvided_PersistsProvidedExpiryDate()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
            ExpiryDate = Today.AddDays(100),
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(Today.AddDays(100), result.ExpiryDate);
    }

    [Fact]
    public async Task Create_WhenPastAwardedOn_Succeeds()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Yesterday,
            EndingOn = Today,
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task Create_WhenManagingOtherUser_ThrowsForbidden()
    {
        var request = new UserTrainingRequest
        {
            UserId = OtherUserId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            InvokeCreateAsync(
                request,
                canManageForOthers: false,
                canEditPast: false,
                canAdjustExpiry: false,
                TestContext.Current.CancellationToken
            )
        );
    }

    // ── Conflict handling ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WhenActiveConflictExists_ThrowsInvalidOperation()
    {
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-30), expiryDate: null);

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeCreateAsync(
                request,
                canManageForOthers: false,
                canEditPast: false,
                canAdjustExpiry: false,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Create_WhenActiveConflictExistsAndOverrideConflicts_SupersedesOldAndCreatesNew()
    {
        var old = await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-30), expiryDate: null);

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
            OverrideConflicts = true,
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Id > 0);
        var oldVersion = await _db.UserTrainings.SingleAsync(
            ut => ut.Id == old.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(Today, oldVersion.ExpiryDate);

        var versions = await _db
            .UserTrainings.Where(ut => ut.UserId == CallerId && ut.TrainingId == TrainingId)
            .OrderBy(ut => ut.AwardedOn)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, versions.Count);
        Assert.Equal(old.Id, versions[0].Id);
        Assert.Equal(result.Id, versions[1].Id);
    }

    [Fact]
    public async Task Create_WhenActiveConflictExistsAndAllowConflictingEvents_CreatesDuplicate()
    {
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-30), expiryDate: null);

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
            AllowConflictingEvents = true,
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        var count = await _db.UserTrainings.CountAsync(
            ut => ut.UserId == CallerId && ut.TrainingId == TrainingId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, count);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task Create_WhenConflictIsExpiredBeforeNewAwardedOn_DoesNotConflict()
    {
        // Expired before the new AwardedOn → not a conflict
        await SeedUserTrainingAsync(
            CallerId,
            TrainingId,
            awardedOn: Today.AddDays(-400),
            expiryDate: Today.AddDays(-10)
        );

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
        };

        var result = await InvokeCreateAsync(
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Id > 0);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenFound_UpdatesAndReturnsResponse()
    {
        var existing = await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today, expiryDate: null);

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
            Notes = "Updated note",
        };

        var result = await InvokeUpdateAsync(
            existing.Id,
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal("Updated note", result!.Notes);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNull()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
        };

        var result = await InvokeUpdateAsync(
            id: 9999,
            request,
            canManageForOthers: false,
            canEditPast: false,
            canAdjustExpiry: false,
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenFound_RemovesAndReturnsTrue()
    {
        var existing = await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today, expiryDate: null);

        var deleted = await InvokeDeleteAsync(
            existing.Id,
            canManageForOthers: false,
            canRemovePast: false,
            TestContext.Current.CancellationToken
        );

        Assert.True(deleted);
        var stillExists = await _db.UserTrainings.AnyAsync(
            ut => ut.Id == existing.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(stillExists);
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsFalse()
    {
        var deleted = await InvokeDeleteAsync(
            id: 9999,
            canManageForOthers: false,
            canRemovePast: false,
            TestContext.Current.CancellationToken
        );

        Assert.False(deleted);
    }

    [Fact]
    public async Task Delete_WhenPastRecord_Succeeds()
    {
        var existing = await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Yesterday, expiryDate: null);

        var deleted = await InvokeDeleteAsync(
            existing.Id,
            canManageForOthers: false,
            canRemovePast: false,
            TestContext.Current.CancellationToken
        );

        Assert.True(deleted);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<UserTrainingResponse> InvokeCreateAsync(
        UserTrainingRequest request,
        bool canManageForOthers,
        bool canEditPast,
        bool canAdjustExpiry,
        CancellationToken cancellationToken
    )
    {
        SetCallerPermissions(canManageForOthers, canEditPast, canRemovePast: false, canAdjustExpiry);
        return _service.CreateAsync(request, CallerId, cancellationToken);
    }

    private Task<UserTrainingResponse?> InvokeUpdateAsync(
        int id,
        UserTrainingRequest request,
        bool canManageForOthers,
        bool canEditPast,
        bool canAdjustExpiry,
        CancellationToken cancellationToken
    )
    {
        SetCallerPermissions(canManageForOthers, canEditPast, canRemovePast: false, canAdjustExpiry);
        return _service.UpdateAsync(id, request, CallerId, cancellationToken);
    }

    private Task<bool> InvokeDeleteAsync(
        int id,
        bool canManageForOthers,
        bool canRemovePast,
        CancellationToken cancellationToken
    )
    {
        SetCallerPermissions(canManageForOthers, canEditPast: false, canRemovePast, canAdjustExpiry: false);
        return _service.DeleteAsync(id, CallerId, cancellationToken);
    }

    private void SetCallerPermissions(
        bool canManageForOthers = false,
        bool canEditPast = false,
        bool canRemovePast = false,
        bool canAdjustExpiry = false
    )
    {
        var claims = new List<Claim> { new(UnifiedClaimTypes.UserId, CallerId.ToString()) };

        _ = canManageForOthers;
        _ = canEditPast;
        _ = canRemovePast;
        _ = canAdjustExpiry;

        var identity = new ClaimsIdentity(claims, "Test");
        _httpContextAccessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private async Task<UserTraining> SeedUserTrainingAsync(
        Guid userId,
        int trainingId,
        DateTimeOffset awardedOn,
        DateTimeOffset? expiryDate
    )
    {
        var entity = new UserTraining
        {
            UserId = userId,
            TrainingId = trainingId,
            AwardedOn = awardedOn,
            ExpiryDate = expiryDate,
            NoticeState = UserTrainingNoticeStates.None,
        };
        _db.UserTrainings.Add(entity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return entity;
    }
}
