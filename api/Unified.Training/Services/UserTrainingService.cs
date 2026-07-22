using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Infrastructure.ErrorHandling;
using Unified.Training.Mappings;
using Unified.Training.Models;

namespace Unified.Training.Services;

public sealed class UserTrainingService(UnifiedDbContext db, IHttpContextAccessor httpContextAccessor)
    : IUserTrainingService
{
    public async Task<IReadOnlyCollection<UserTrainingResponse>> GetAllAsync(
        Guid userId,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthorizedToManageFor(userId, callerUserId);

        return await db
            .UserTrainings.Where(ut => ut.UserId == userId)
            .OrderByDescending(ut => ut.AwardedOn)
            .ThenByDescending(ut => ut.Id)
            .ProjectToType<UserTrainingResponse>(UserTrainingMappings.ResponseConfig)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserTrainingResponse?> GetByTrainingAndUserAsync(
        int trainingId,
        Guid userId,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthorizedToManageFor(userId, callerUserId);

        return await db
            .UserTrainings.Where(ut => ut.UserId == userId && ut.TrainingId == trainingId)
            .OrderByDescending(ut => ut.AwardedOn)
            .ThenByDescending(ut => ut.Id)
            .ProjectToType<UserTrainingResponse>(UserTrainingMappings.ResponseConfig)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserTrainingResponse> CreateAsync(
        UserTrainingRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthorizedToManageFor(request.UserId, callerUserId);

        await HandleConflictsAsync(request, existingId: null, cancellationToken);

        var expiryDate = request.ExpiryDate ?? await CalculateExpiryDateAsync(request, cancellationToken);

        var entity = new UserTraining
        {
            UserId = request.UserId,
            TrainingId = request.TrainingId,
            AwardedOn = request.AwardedOn,
            EndingOn = request.EndingOn,
            ExpiryDate = expiryDate,
            Notes = request.Notes?.Trim(),
            NoticeState = UserTrainingNoticeStates.None,
        };

        db.UserTrainings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return await FetchResponseAsync(entity.Id, cancellationToken);
    }

    public async Task<UserTrainingResponse?> UpdateAsync(
        int id,
        UserTrainingRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.UserTrainings.SingleOrDefaultAsync(ut => ut.Id == id, cancellationToken);
        if (entity is null)
            return null;

        EnsureAuthorizedToManageFor(entity.UserId, callerUserId);
        EnsureAuthorizedToManageFor(request.UserId, callerUserId);

        await HandleConflictsAsync(request, existingId: id, cancellationToken);

        var expiryDate = request.ExpiryDate ?? await CalculateExpiryDateAsync(request, cancellationToken);

        entity.UserId = request.UserId;
        entity.TrainingId = request.TrainingId;
        entity.AwardedOn = request.AwardedOn;
        entity.EndingOn = request.EndingOn;
        entity.ExpiryDate = expiryDate;
        entity.Notes = request.Notes?.Trim();

        await db.SaveChangesAsync(cancellationToken);

        return await FetchResponseAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var entity = await db.UserTrainings.SingleOrDefaultAsync(ut => ut.Id == id, cancellationToken);
        if (entity is null)
            return false;

        EnsureAuthorizedToManageFor(entity.UserId, callerUserId);

        db.UserTrainings.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Resolves conflicts for the given request. Behaviour depends on the request flags:
    /// <list type="bullet">
    ///   <item><c>OverrideConflicts</c>: supersedes any existing active records for the same user + training type (excluding <paramref name="existingId"/>) by ending them when the new record begins.</item>
    ///   <item><c>AllowConflictingEvents</c>: proceeds without checking or removing conflicts.</item>
    ///   <item>Neither: throws <see cref="InvalidOperationException"/> if an active conflicting record is found.</item>
    /// </list>
    /// A conflict is an existing record where <c>ExpiryDate</c> is null (always valid) or
    /// <c>ExpiryDate</c> is after <c>request.AwardedOn</c> (still valid when the new training is awarded).
    /// </summary>
    private async Task HandleConflictsAsync(
        UserTrainingRequest request,
        int? existingId,
        CancellationToken cancellationToken
    )
    {
        var conflictQuery = db.UserTrainings.Where(ut =>
            ut.UserId == request.UserId
            && ut.TrainingId == request.TrainingId
            && (existingId == null || ut.Id != existingId)
            && (ut.ExpiryDate == null || ut.ExpiryDate > request.AwardedOn)
        );

        if (request.OverrideConflicts)
        {
            var conflicting = await conflictQuery.ToListAsync(cancellationToken);

            foreach (var conflict in conflicting)
            {
                conflict.ExpiryDate = request.AwardedOn;
            }

            return;
        }

        if (request.AllowConflictingEvents)
            return;

        var hasConflict = await conflictQuery.AnyAsync(cancellationToken);
        if (hasConflict)
            throw new InvalidOperationException(
                "An active training record already exists for this user and training type. "
                    + "Use OverrideConflicts to replace it, or AllowConflictingEvents to create alongside it."
            );
    }

    /// <summary>
    /// Auto-calculates the expiry date from the training type's <c>ValidityDays</c>.
    /// Returns <c>null</c> when the training has no validity period (always valid).
    /// </summary>
    private async Task<DateTimeOffset?> CalculateExpiryDateAsync(
        UserTrainingRequest request,
        CancellationToken cancellationToken
    )
    {
        var validityDays = await db
            .Trainings.Where(t => t.Id == request.TrainingId)
            .Select(t => t.ValidityDays)
            .SingleOrDefaultAsync(cancellationToken);

        return validityDays.HasValue ? request.AwardedOn.AddDays(validityDays.Value) : null;
    }

    private static void EnsureAuthorizedToManageFor(Guid targetUserId, Guid callerUserId)
    {
        if (targetUserId != callerUserId)
            throw new ForbiddenException();
    }

    private async Task<UserTrainingResponse> FetchResponseAsync(int id, CancellationToken cancellationToken) =>
        await db
            .UserTrainings.Where(ut => ut.Id == id)
            .ProjectToType<UserTrainingResponse>(UserTrainingMappings.ResponseConfig)
            .SingleAsync(cancellationToken);
}
