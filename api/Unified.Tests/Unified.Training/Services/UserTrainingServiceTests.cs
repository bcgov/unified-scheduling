using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;
using Unified.Training.Controllers;
using Unified.Training.Models;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Tests.Training.Services;

public class UserTrainingServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"user-training-service-{Guid.NewGuid():N}";
    private UnifiedDbContext _db = null!;
    private UserTrainingService _service = null!;

    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private const int TrainingId = 1;
    private const int TrainingWithValidityId = 2;
    private const int NonRotatingTrainingId = 3;
    private const int ExpiredTrainingId = 4;
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

        _service = new UserTrainingService(_db);

        await SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        _db.Locations.Add(
            new Location
            {
                Id = 1,
                AgencyId = "TEST",
                Name = "Test",
                Timezone = "UTC",
            }
        );
        _db.Users.Add(
            new User
            {
                Id = CallerId,
                HomeLocationId = 1,
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
                HomeLocationId = 1,
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
                Rotating = true,
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
                Rotating = true,
            }
        );

        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = NonRotatingTrainingId,
                Code = "NR",
                Description = "Non-Rotating Training",
                TrainingCategoryId = 1,
                Rotating = false,
            }
        );

        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = ExpiredTrainingId,
                Code = "OLD",
                Description = "Expired Training",
                TrainingCategoryId = 1,
                Rotating = true,
                ExpiryDate = Today.AddDays(-1),
            }
        );

        await _db.SaveChangesAsync();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WhenAllDayIsOmitted_PersistsLegacyAllDayDates()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = new DateTimeOffset(2026, 7, 20, 15, 0, 0, TimeSpan.FromHours(-7)),
            EndingOn = new DateTimeOffset(2026, 7, 20, 15, 0, 0, TimeSpan.FromHours(-7)),
        };
        var controller = new UserTrainingController(_service, new UserTrainingRequestValidator());

        var action = await controller.Create(request, TestContext.Current.CancellationToken);

        var created = Assert.IsType<UserTrainingResponse>(Assert.IsType<CreatedResult>(action.Result).Value);
        Assert.True(created.AllDay);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), created.AwardedOn);
        Assert.Equal(created.AwardedOn, created.EndingOn);
        Assert.True(
            (
                await _db.UserTrainings.SingleAsync(
                    item => item.Id == created.Id,
                    TestContext.Current.CancellationToken
                )
            ).AllDay
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_WhenAllDayIsOmitted_PreservesStoredClassification(bool allDay)
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.FromHours(-7)),
            EndingOn = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.FromHours(-7)),
            AllDay = allDay,
        };
        var created = await _service.CreateAsync(request, TestContext.Current.CancellationToken);

        var updated = await _service.UpdateAsync(
            created.Id,
            request with
            {
                AllDay = null,
                Notes = "Legacy update",
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(updated);
        Assert.Equal(allDay, updated.AllDay);
        Assert.Equal(created.AwardedOn, updated.AwardedOn);
        Assert.Equal(created.EndingOn, updated.EndingOn);
        Assert.Equal("Legacy update", updated.Notes);
        Assert.Equal(
            allDay,
            (
                await _db.UserTrainings.SingleAsync(
                    item => item.Id == created.Id,
                    TestContext.Current.CancellationToken
                )
            ).AllDay
        );
    }

    [Fact]
    public async Task Update_OmittedTimedClassification_ValidatesExpiryAgainstTheTimedUtcEnd()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AllDay = false,
            AwardedOn = new DateTimeOffset(2026, 7, 20, 23, 30, 0, TimeSpan.FromHours(14)),
            EndingOn = new DateTimeOffset(2026, 7, 21, 0, 30, 0, TimeSpan.FromHours(14)),
        };
        var created = await _service.CreateAsync(request, TestContext.Current.CancellationToken);
        var controller = new UserTrainingController(_service, new UserTrainingRequestValidator());
        var update = request with
        {
            AllDay = null,
            ExpiryDate = new DateTimeOffset(2026, 7, 20, 11, 0, 0, TimeSpan.Zero),
        };

        var action = await controller.Update(created.Id, update, TestContext.Current.CancellationToken);

        var updated = Assert.IsType<UserTrainingResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.False(updated.AllDay);
        Assert.Equal(request.AwardedOn.ToUniversalTime(), updated.AwardedOn);
        Assert.Equal(request.EndingOn.ToUniversalTime(), updated.EndingOn);
        Assert.Equal(update.ExpiryDate, updated.ExpiryDate);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.Update(
                created.Id,
                update with
                {
                    EndingOn = update.AwardedOn,
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(
            updated.EndingOn,
            (
                await _db.UserTrainings.SingleAsync(
                    item => item.Id == created.Id,
                    TestContext.Current.CancellationToken
                )
            ).EndingOn
        );
    }

    [Fact]
    public async Task CreateUpdateAndRenew_ExplicitClassification_RoundTripsThroughResponses()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AllDay = true,
            AwardedOn = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.FromHours(14)),
            EndingOn = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.FromHours(14)),
        };
        var created = await _service.CreateAsync(request, TestContext.Current.CancellationToken);
        Assert.True(created.AllDay);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), created.AwardedOn);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero), created.EndingOn);

        var timedRequest = request with { AllDay = false };
        var updated = await _service.UpdateAsync(created.Id, timedRequest, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.False(updated.AllDay);
        Assert.Equal(request.AwardedOn.ToUniversalTime(), updated.AwardedOn);
        Assert.Equal(request.EndingOn.ToUniversalTime(), updated.EndingOn);
        Assert.False(
            Assert.Single(await _service.GetUserTrainings(CallerId, TestContext.Current.CancellationToken)).AllDay
        );
        Assert.False(
            (
                await _service.GetByTrainingAndUserAsync(TrainingId, CallerId, TestContext.Current.CancellationToken)
            )!.AllDay
        );

        var renewed = await _service.CreateAsync(request, TestContext.Current.CancellationToken);
        Assert.True(renewed.AllDay);
        Assert.Equal(2, renewed.Version);
        Assert.True(
            (
                await _service.GetByTrainingAndUserAsync(TrainingId, CallerId, TestContext.Current.CancellationToken)
            )!.AllDay
        );
        Assert.False(
            (
                await _db.UserTrainings.SingleAsync(
                    item => item.Id == created.Id,
                    TestContext.Current.CancellationToken
                )
            ).AllDay
        );
    }

    [Fact]
    public async Task GetAll_WhenCallerOwnsRecords_ReturnsNewestFirst()
    {
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-10), expiryDate: null);
        await SeedUserTrainingAsync(CallerId, TrainingWithValidityId, awardedOn: Today, expiryDate: Today.AddDays(365));

        var result = await _service.GetUserTrainings(CallerId, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(TrainingWithValidityId, result.First().TrainingId);
        Assert.Equal(TrainingId, result.Last().TrainingId);
    }

    [Fact]
    public async Task GetAll_WhenViewingOtherUser_ReturnsRecords()
    {
        await SeedUserTrainingAsync(OtherUserId, TrainingId, awardedOn: Today, expiryDate: null);

        var result = await _service.GetUserTrainings(OtherUserId, TestContext.Current.CancellationToken);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetByTrainingAndUser_WhenMultipleVersionsExist_ReturnsLatestVersion()
    {
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-10), expiryDate: Today.AddDays(-1));
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today, expiryDate: Today.AddDays(10));

        var result = await _service.GetByTrainingAndUserAsync(
            TrainingId,
            CallerId,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(2, result!.Version);
        Assert.Equal(Today, result.AwardedOn);
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
        Assert.Equal(
            new DateTimeOffset(Tomorrow.Year, 12, 31, 23, 59, 59, 999, TimeSpan.Zero),
            result.ExpiryDate!.Value
        );
    }

    [Theory]
    [InlineData(false, 30)]
    [InlineData(true, 30)]
    [InlineData(false, 365)]
    [InlineData(true, 365)]
    public async Task Save_WhenExpiryIsAutomatic_CalculatesFromEndingOn(bool isUpdate, int validityDays)
    {
        var training = await _db.Trainings.SingleAsync(
            item => item.Id == TrainingWithValidityId,
            TestContext.Current.CancellationToken
        );
        training.ValidityDays = validityDays;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingWithValidityId,
            AllDay = false,
            AwardedOn = new DateTimeOffset(2025, 12, 30, 15, 0, 0, TimeSpan.FromHours(-8)),
            EndingOn = new DateTimeOffset(2026, 1, 2, 18, 0, 0, TimeSpan.FromHours(-8)),
        };

        UserTrainingResponse? result;
        if (isUpdate)
        {
            var existing = await SeedUserTrainingAsync(
                CallerId,
                TrainingWithValidityId,
                request.AwardedOn.ToUniversalTime(),
                expiryDate: null
            );
            result = await _service.UpdateAsync(existing.Id, request, TestContext.Current.CancellationToken);
        }
        else
        {
            result = await _service.CreateAsync(request, TestContext.Current.CancellationToken);
        }

        var expectedExpiry =
            validityDays == 365
                ? new DateTimeOffset(2026, 12, 31, 23, 59, 59, 999, TimeSpan.Zero)
                : new DateTimeOffset(2026, 2, 2, 2, 0, 0, TimeSpan.Zero);
        Assert.NotNull(result);
        Assert.Equal(expectedExpiry, result.ExpiryDate);
        var persisted = await _db
            .UserTrainings.AsNoTracking()
            .SingleAsync(item => item.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.Equal(expectedExpiry, persisted.ExpiryDate);
    }

    [Fact]
    public async Task CalculateExpiryDate_WhenTrainingHasValidityDays_ReturnsCalculatedExpiryDate()
    {
        var request = new UserTrainingExpiryDateRequest
        {
            TrainingId = TrainingWithValidityId,
            AwardedOn = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var result = await _service.CalculateExpiryDateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero), result.ExpiryDate);
    }

    [Fact]
    public async Task CalculateExpiryDate_WhenTrainingHasNoValidityDays_ReturnsNull()
    {
        var request = new UserTrainingExpiryDateRequest
        {
            TrainingId = TrainingId,
            AwardedOn = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var result = await _service.CalculateExpiryDateAsync(request, TestContext.Current.CancellationToken);

        Assert.Null(result.ExpiryDate);
    }

    [Theory]
    [InlineData("America/Vancouver", true, 30)]
    [InlineData("America/Vancouver", false, 30)]
    [InlineData(
        "America/Vancouver",
        true,
        365,
        Skip = "Temporary: NuGet mapper does not yet support B.C.'s updated IANA timezone mapping."
    )]
    [InlineData("America/Vancouver", false, 365)]
    [InlineData("America/Edmonton", true, 30)]
    [InlineData("America/Edmonton", false, 30)]
    [InlineData("America/Edmonton", true, 365)]
    [InlineData("America/Edmonton", false, 365)]
    public async Task ExpiryPreview_CreateUpdateAndRenew_UseTheSameUserTimezoneCalculation(
        string timeZoneId,
        bool allDay,
        int validityDays
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var location = await _db.Locations.SingleAsync(cancellationToken);
        location.Timezone = timeZoneId;
        var rules = await _db.Trainings.SingleAsync(item => item.Id == TrainingWithValidityId, cancellationToken);
        rules.ValidityDays = validityDays;
        await _db.SaveChangesAsync(cancellationToken);
        var endingOn = new DateTimeOffset(2026, 7, 20, allDay ? 0 : 23, 0, 0, TimeSpan.Zero);
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingWithValidityId,
            AwardedOn = allDay ? endingOn : endingOn.AddHours(-1),
            EndingOn = endingOn,
            AllDay = allDay,
        };
        var preview = await _service.CalculateExpiryDateAsync(
            new UserTrainingExpiryDateRequest
            {
                TrainingId = request.TrainingId,
                UserId = CallerId,
                AllDay = allDay,
                AwardedOn = endingOn,
            },
            cancellationToken
        );
        var created = await _service.CreateAsync(request, cancellationToken);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var expectedLocalDate = validityDays == 365 ? new DateTime(2026, 12, 31) : new DateTime(2026, 8, 19);
        var localExpiry = TimeZoneInfo.ConvertTime(created.ExpiryDate!.Value, zone);
        Assert.Equal(expectedLocalDate, localExpiry.Date);
        if (allDay || validityDays == 365)
            Assert.Equal(new TimeSpan(0, 23, 59, 59, 999), localExpiry.TimeOfDay);
        else
            Assert.Equal(endingOn.AddDays(30), created.ExpiryDate);
        Assert.Equal(preview.ExpiryDate, created.ExpiryDate);
        if (allDay && timeZoneId == "America/Vancouver" && validityDays == 365)
            Assert.Equal(new DateTimeOffset(2027, 1, 1, 6, 59, 59, 999, TimeSpan.Zero), created.ExpiryDate);

        var updated = await _service.UpdateAsync(
            created.Id,
            request with
            {
                Notes = "unchanged dates",
            },
            cancellationToken
        );
        Assert.NotNull(updated);
        Assert.Equal(preview.ExpiryDate, updated.ExpiryDate);

        var renewal = request with { AwardedOn = request.AwardedOn.AddYears(1), EndingOn = endingOn.AddYears(1) };
        var renewalPreview = await _service.CalculateExpiryDateAsync(
            new UserTrainingExpiryDateRequest
            {
                TrainingId = request.TrainingId,
                UserId = CallerId,
                AllDay = allDay,
                AwardedOn = renewal.EndingOn,
            },
            cancellationToken
        );
        var renewed = await _service.CreateAsync(renewal, cancellationToken);
        Assert.Equal(2, renewed.Version);
        Assert.Equal(renewalPreview.ExpiryDate, renewed.ExpiryDate);
        var persisted = await _db
            .UserTrainings.AsNoTracking()
            .SingleAsync(item => item.Id == renewed.Id, cancellationToken);
        Assert.Equal(renewalPreview.ExpiryDate, persisted.ExpiryDate);
    }

    [Fact]
    public async Task ExpiryPreview_TimedAnnualTraining_UsesTheLocalCompletionYear()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        (await _db.Locations.SingleAsync(cancellationToken)).Timezone = "America/Vancouver";
        await _db.SaveChangesAsync(cancellationToken);

        var preview = await _service.CalculateExpiryDateAsync(
            new UserTrainingExpiryDateRequest
            {
                UserId = CallerId,
                TrainingId = TrainingWithValidityId,
                AllDay = false,
                AwardedOn = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.Zero),
            },
            cancellationToken
        );

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 7, 59, 59, 999, TimeSpan.Zero), preview.ExpiryDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public async Task ExpiryPreviewAndSave_WithoutValidHomeTimezone_RejectTimezoneDependentOperations(string? zone)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = await _db.Users.SingleAsync(item => item.Id == CallerId, cancellationToken);
        if (zone is null)
            user.HomeLocationId = null;
        else
            (await _db.Locations.SingleAsync(cancellationToken)).Timezone = zone;
        await _db.SaveChangesAsync(cancellationToken);
        var endingOn = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingWithValidityId,
            AllDay = true,
            AwardedOn = endingOn,
            EndingOn = endingOn,
        };

        var previewError = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CalculateExpiryDateAsync(
                new UserTrainingExpiryDateRequest
                {
                    UserId = CallerId,
                    TrainingId = request.TrainingId,
                    AllDay = true,
                    AwardedOn = endingOn,
                },
                cancellationToken
            )
        );
        Assert.Contains("home location with a valid timezone", previewError.Message);
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, cancellationToken));
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(
                request with
                {
                    TrainingId = TrainingId,
                    AllDay = false,
                    EndingOn = endingOn.AddHours(1),
                },
                cancellationToken
            )
        );
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(
                request with
                {
                    TrainingId = TrainingId,
                    ExpiryDate = endingOn.AddDays(1),
                },
                cancellationToken
            )
        );
        Assert.Empty(await _db.UserTrainings.ToListAsync(cancellationToken));

        var allDayWithoutExpiry = await _service.CreateAsync(
            request with
            {
                TrainingId = TrainingId,
            },
            cancellationToken
        );
        Assert.True(allDayWithoutExpiry.AllDay);
        Assert.Null(allDayWithoutExpiry.ExpiryDate);
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(
                allDayWithoutExpiry.Id,
                request with
                {
                    TrainingId = TrainingId,
                    AllDay = false,
                    EndingOn = endingOn.AddHours(1),
                },
                cancellationToken
            )
        );
        var unchanged = await _db.UserTrainings.AsNoTracking().SingleAsync(cancellationToken);
        Assert.True(unchanged.AllDay);
        Assert.Equal(endingOn, unchanged.EndingOn);
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
    public async Task Create_WhenManagingOtherUser_Succeeds()
    {
        var request = new UserTrainingRequest
        {
            UserId = OtherUserId,
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
        Assert.Equal(OtherUserId, result.UserId);
    }

    [Fact]
    public async Task Create_WhenPriorVersionExists_AssignsNextVersion()
    {
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-20), expiryDate: Today.AddDays(-5));

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

        Assert.Equal(2, result.Version);
    }

    [Fact]
    public async Task Create_WhenExpiryNotAfterPreviousVersion_ThrowsInvalidOperationException()
    {
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-20), expiryDate: Today.AddDays(10));

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
            ExpiryDate = Today.AddDays(5),
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
    public async Task Create_WhenTrainingIsNonRotatingAndPriorVersionExists_ThrowsInvalidOperationException()
    {
        await SeedUserTrainingAsync(CallerId, NonRotatingTrainingId, awardedOn: Today.AddDays(-20), expiryDate: null);

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = NonRotatingTrainingId,
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
    public async Task Create_WhenTrainingTypeIsExpired_ThrowsInvalidOperationException()
    {
        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = ExpiredTrainingId,
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

    [Fact]
    public async Task Update_WhenAttemptingToChangeTrainingId_ThrowsInvalidOperationException()
    {
        var existing = await SeedUserTrainingAsync(
            CallerId,
            TrainingId,
            awardedOn: Today,
            expiryDate: Today.AddDays(30)
        );

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingWithValidityId,
            AwardedOn = Today,
            EndingOn = Tomorrow,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeUpdateAsync(
                existing.Id,
                request,
                canManageForOthers: false,
                canEditPast: false,
                canAdjustExpiry: false,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Update_WhenExpiryNotBeforeNextVersion_ThrowsInvalidOperationException()
    {
        var first = await SeedUserTrainingAsync(
            CallerId,
            TrainingId,
            awardedOn: Today.AddDays(-20),
            expiryDate: Today.AddDays(2)
        );
        await SeedUserTrainingAsync(CallerId, TrainingId, awardedOn: Today.AddDays(-10), expiryDate: Today.AddDays(12));

        var request = new UserTrainingRequest
        {
            UserId = CallerId,
            TrainingId = TrainingId,
            AwardedOn = Today.AddDays(-20),
            EndingOn = Today.AddDays(-19),
            ExpiryDate = Today.AddDays(20),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeUpdateAsync(
                first.Id,
                request,
                canManageForOthers: false,
                canEditPast: false,
                canAdjustExpiry: false,
                TestContext.Current.CancellationToken
            )
        );
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
        _ = canManageForOthers;
        _ = canEditPast;
        _ = canAdjustExpiry;
        return _service.CreateAsync(request, cancellationToken);
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
        _ = canManageForOthers;
        _ = canEditPast;
        _ = canAdjustExpiry;
        return _service.UpdateAsync(id, request, cancellationToken);
    }

    private Task<bool> InvokeDeleteAsync(
        int id,
        bool canManageForOthers,
        bool canRemovePast,
        CancellationToken cancellationToken
    )
    {
        _ = canManageForOthers;
        _ = canRemovePast;
        return _service.DeleteAsync(id, cancellationToken);
    }

    private async Task<UserTraining> SeedUserTrainingAsync(
        Guid userId,
        int trainingId,
        DateTimeOffset awardedOn,
        DateTimeOffset? expiryDate
    )
    {
        var nextVersion =
            await _db
                .UserTrainings.Where(ut => ut.UserId == userId && ut.TrainingId == trainingId)
                .Select(ut => (int?)ut.Version)
                .MaxAsync()
            ?? 0;

        var entity = new UserTraining
        {
            UserId = userId,
            TrainingId = trainingId,
            Version = nextVersion + 1,
            AwardedOn = awardedOn,
            EndingOn = awardedOn.AddDays(1),
            ExpiryDate = expiryDate,
            NoticeState = UserTrainingNoticeStates.None,
        };
        _db.UserTrainings.Add(entity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return entity;
    }
}
