using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Training.Helpers;
using Unified.Training.Mappings;
using Unified.Training.Models;

namespace Unified.Training.Services;

public sealed class UserTrainingService(
    UnifiedDbContext db,
    ICalendarConflictService? conflictService = null,
    TimeProvider? timeProvider = null
) : IUserTrainingService
{
    private static readonly DateTimeOffset MinDateTimeOffsetUtc = DateTimeOffset.MinValue;

    public async Task<IReadOnlyCollection<UserTrainingResponse>> GetUserTrainings(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .UserTrainings.Where(ut => ut.UserId == userId)
            .OrderByDescending(ut => ut.AwardedOn)
            .ThenByDescending(ut => ut.Version)
            .ThenByDescending(ut => ut.Id)
            .Select(ut => new UserTrainingResponse
            {
                Id = ut.Id,
                UserId = ut.UserId,
                TrainingId = ut.TrainingId,
                Version = ut.Version,
                TrainingCode = ut.Training.Code,
                TrainingCategoryName =
                    ut.Training.TrainingCategory != null ? ut.Training.TrainingCategory.Name : string.Empty,
                AwardedOn = ut.AwardedOn,
                EndingOn = ut.EndingOn == MinDateTimeOffsetUtc ? ut.AwardedOn : ut.EndingOn,
                ExpiryDate = ut.ExpiryDate,
                NoticeState = ut.NoticeState,
                Notes = ut.Notes,
                CreatedOn = ut.CreatedOn,
                UpdatedOn = ut.UpdatedOn,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<UserTrainingResponse?> GetByTrainingAndUserAsync(
        int trainingId,
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        return db
            .UserTrainings.Where(ut => ut.UserId == userId && ut.TrainingId == trainingId)
            .OrderByDescending(ut => ut.Version)
            .ThenByDescending(ut => ut.Id)
            .Select(ut => new UserTrainingResponse
            {
                Id = ut.Id,
                UserId = ut.UserId,
                TrainingId = ut.TrainingId,
                Version = ut.Version,
                TrainingCode = ut.Training.Code,
                TrainingCategoryName =
                    ut.Training.TrainingCategory != null ? ut.Training.TrainingCategory.Name : string.Empty,
                AwardedOn = ut.AwardedOn,
                EndingOn = ut.EndingOn == MinDateTimeOffsetUtc ? ut.AwardedOn : ut.EndingOn,
                ExpiryDate = ut.ExpiryDate,
                NoticeState = ut.NoticeState,
                Notes = ut.Notes,
                CreatedOn = ut.CreatedOn,
                UpdatedOn = ut.UpdatedOn,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserTrainingExpiryDateResponse> CalculateExpiryDateAsync(
        UserTrainingExpiryDateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var trainingRules = await GetTrainingRulesAsync(request.TrainingId, cancellationToken: cancellationToken);
        var awardedOnUtc = request.AwardedOn.ToUniversalTime();

        return new UserTrainingExpiryDateResponse
        {
            ExpiryDate = UserTrainingHelper.CalculateExpiryDate(awardedOnUtc, trainingRules.ValidityDays),
        };
    }

    public async Task<UserTrainingResponse> CreateAsync(
        UserTrainingRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedRequest = UserTrainingHelper.NormalizeToUtc(request);

        var trainingRules = await GetTrainingRulesAsync(
            normalizedRequest.TrainingId,
            includeExpired: false,
            cancellationToken
        );

        var latestVersion = await GetLatestVersionAsync(
            normalizedRequest.UserId,
            normalizedRequest.TrainingId,
            cancellationToken
        );

        EnsureTrainingAllowsRenewal(trainingRules.Rotating, latestVersion);

        var validityDays = trainingRules.ValidityDays;
        var expiryDate =
            normalizedRequest.ExpiryDate
            ?? UserTrainingHelper.CalculateExpiryDate(normalizedRequest.AwardedOn, validityDays);

        EnsureExpiryIsAfterPreviousVersion(expiryDate, latestVersion?.ExpiryDate);

        var entity = MapToEntity(normalizedRequest, expiryDate);
        entity.Version = latestVersion is null ? 1 : latestVersion.Version + 1;

        db.UserTrainings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return await FetchResponseAsync(entity.Id, cancellationToken);
    }

    public async Task<UserTrainingResponse?> UpdateAsync(
        int id,
        UserTrainingRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedRequest = UserTrainingHelper.NormalizeToUtc(request);

        var entity = await db.UserTrainings.SingleOrDefaultAsync(ut => ut.Id == id, cancellationToken);
        if (entity is null)
            return null;

        if (normalizedRequest.UserId != entity.UserId || normalizedRequest.TrainingId != entity.TrainingId)
            throw new InvalidOperationException("UserId and TrainingId cannot be changed for an existing version.");

        var trainingRules = await GetTrainingRulesAsync(
            normalizedRequest.TrainingId,
            cancellationToken: cancellationToken
        );
        var validityDays = trainingRules.ValidityDays;
        var expiryDate =
            normalizedRequest.ExpiryDate
            ?? UserTrainingHelper.CalculateExpiryDate(normalizedRequest.AwardedOn, validityDays);

        var previousVersionExpiryDate = await db
            .UserTrainings.Where(ut =>
                ut.UserId == entity.UserId && ut.TrainingId == entity.TrainingId && ut.Version < entity.Version
            )
            .OrderByDescending(ut => ut.Version)
            .Select(ut => ut.ExpiryDate)
            .FirstOrDefaultAsync(cancellationToken);

        var nextVersionExpiryDate = await db
            .UserTrainings.Where(ut =>
                ut.UserId == entity.UserId && ut.TrainingId == entity.TrainingId && ut.Version > entity.Version
            )
            .OrderBy(ut => ut.Version)
            .Select(ut => ut.ExpiryDate)
            .FirstOrDefaultAsync(cancellationToken);

        EnsureExpiryIsAfterPreviousVersion(expiryDate, previousVersionExpiryDate);
        EnsureExpiryIsBeforeNextVersion(expiryDate, nextVersionExpiryDate);

        var datesChanged =
            entity.AwardedOn != normalizedRequest.AwardedOn || entity.EndingOn != normalizedRequest.EndingOn;
        await using var transaction =
            datesChanged && db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
        var previousExpiryDate = entity.ExpiryDate;
        MapToEntity(normalizedRequest, entity, expiryDate);

        if (previousExpiryDate != expiryDate)
        {
            entity.NoticeState = UserTrainingNoticeStates.None;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (datesChanged)
            await InvalidateOverridesAsync(entity.Id, cancellationToken);

        var result = await FetchResponseAsync(entity.Id, cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.UserTrainings.SingleOrDefaultAsync(ut => ut.Id == id, cancellationToken);
        if (entity is null)
            return false;

        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        db.UserTrainings.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateOverridesAsync(entity.Id, cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private async Task InvalidateOverridesAsync(int userTrainingId, CancellationToken cancellationToken)
    {
        var identity = TrainingConflictParticipantProvider.IdentityFor(userTrainingId);
        if (conflictService is not null)
        {
            await conflictService.InvalidateResolvedOverridesAsync([identity], cancellationToken: cancellationToken);
            return;
        }

        var overrides = await db
            .CalendarConflictOverrides.Where(overrideEntity =>
                overrideEntity.InvalidatedOn == null
                && (
                    (
                        overrideEntity.FirstSourceModule == identity.SourceModule
                        && overrideEntity.FirstEventId == identity.EventId
                    )
                    || (
                        overrideEntity.SecondSourceModule == identity.SourceModule
                        && overrideEntity.SecondEventId == identity.EventId
                    )
                )
            )
            .ToListAsync(cancellationToken);
        if (overrides.Count == 0)
            return;

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        foreach (var overrideEntity in overrides)
        {
            overrideEntity.InvalidatedOn = now;
            overrideEntity.UpdatedOn = now;
            overrideEntity.UpdatedById = null;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static UserTraining MapToEntity(UserTrainingRequest request, DateTimeOffset? expiryDate)
    {
        var entity = request.Adapt<UserTraining>(UserTrainingMappings.RequestToEntityConfig);
        entity.ExpiryDate = expiryDate;
        entity.NoticeState = UserTrainingNoticeStates.None;

        return entity;
    }

    private static void MapToEntity(UserTrainingRequest request, UserTraining entity, DateTimeOffset? expiryDate)
    {
        request.Adapt(entity, UserTrainingMappings.RequestToEntityConfig);
        entity.ExpiryDate = expiryDate;
    }

    private static void EnsureExpiryIsAfterPreviousVersion(
        DateTimeOffset? expiryDate,
        DateTimeOffset? previousVersionExpiryDate
    )
    {
        if (!previousVersionExpiryDate.HasValue || !expiryDate.HasValue)
            return;

        if (expiryDate <= previousVersionExpiryDate)
            throw new InvalidOperationException("Expiry date must be later than the previous version expiry date.");
    }

    private static void EnsureExpiryIsBeforeNextVersion(
        DateTimeOffset? expiryDate,
        DateTimeOffset? nextVersionExpiryDate
    )
    {
        if (!nextVersionExpiryDate.HasValue)
            return;

        if (!expiryDate.HasValue || expiryDate >= nextVersionExpiryDate)
            throw new InvalidOperationException("Expiry date must be earlier than the next version expiry date.");
    }

    private static void EnsureTrainingAllowsRenewal(bool rotating, UserTraining? latestVersion)
    {
        if (latestVersion is not null && !rotating)
            throw new InvalidOperationException("Cannot create a new version for a non-rotating training.");
    }

    private async Task<UserTraining?> GetLatestVersionAsync(
        Guid userId,
        int trainingId,
        CancellationToken cancellationToken
    ) =>
        await db
            .UserTrainings.Where(ut => ut.UserId == userId && ut.TrainingId == trainingId)
            .OrderByDescending(ut => ut.Version)
            .ThenByDescending(ut => ut.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<TrainingRules> GetTrainingRulesAsync(
        int trainingId,
        bool includeExpired = true,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTimeOffset.UtcNow;

        var rules = await db
            .Trainings.Where(t => t.Id == trainingId)
            .Where(t => includeExpired || t.ExpiryDate == null || t.ExpiryDate > now)
            .Select(t => new TrainingRules(t.ValidityDays, t.Rotating))
            .SingleOrDefaultAsync(cancellationToken);

        return rules
            ?? throw new InvalidOperationException(
                includeExpired
                    ? $"Training {trainingId} was not found."
                    : $"Training {trainingId} is expired and cannot be assigned."
            );
    }

    private sealed record TrainingRules(int? ValidityDays, bool Rotating);

    private async Task<UserTrainingResponse> FetchResponseAsync(int id, CancellationToken cancellationToken) =>
        await db
            .UserTrainings.Where(ut => ut.Id == id)
            .Select(ut => new UserTrainingResponse
            {
                Id = ut.Id,
                UserId = ut.UserId,
                TrainingId = ut.TrainingId,
                Version = ut.Version,
                TrainingCode = ut.Training.Code,
                TrainingCategoryName =
                    ut.Training.TrainingCategory != null ? ut.Training.TrainingCategory.Name : string.Empty,
                AwardedOn = ut.AwardedOn,
                EndingOn = ut.EndingOn == MinDateTimeOffsetUtc ? ut.AwardedOn : ut.EndingOn,
                ExpiryDate = ut.ExpiryDate,
                NoticeState = ut.NoticeState,
                Notes = ut.Notes,
                CreatedOn = ut.CreatedOn,
                UpdatedOn = ut.UpdatedOn,
            })
            .SingleAsync(cancellationToken);
}
