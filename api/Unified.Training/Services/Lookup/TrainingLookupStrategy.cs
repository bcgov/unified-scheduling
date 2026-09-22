using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;
using Unified.Training.Models;
using TrainingEntity = Unified.Db.Models.Training.Training;
using TrainingProfileLinkEntity = Unified.Db.Models.Training.TrainingProfile;

namespace Unified.Training.Services.Lookup;

public sealed class TrainingLookupStrategy(UnifiedDbContext db) : ITrainingLookupStrategy
{
    public LookupCodeTypes CodeType => LookupCodeTypes.Trainings;

    public async Task<IReadOnlyCollection<TrainingLookupResponse>> GetAllTrainingsAsync(
        bool includeExpired = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = db.Trainings.AsNoTracking().AsQueryable();

        if (!includeExpired)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(t => t.ExpiryDate == null || t.ExpiryDate > now);
        }

        return await BuildResponseQuery(query.OrderBy(t => t.Order).ThenBy(t => t.Code)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var results = await GetAllTrainingsAsync(cancellationToken: cancellationToken);
        return results;
    }

    public async Task<TrainingLookupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await BuildResponseQuery(db.Trainings.AsNoTracking().Where(t => t.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TrainingLookupResponse> CreateAsync(
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedRequest = NormalizeRequest(request);
        var profileTypeIds = ResolveMandatoryProfileTypeIds(normalizedRequest);

        var entity = normalizedRequest.Adapt<TrainingEntity>();
        if (entity.EffectiveDate == default)
        {
            entity.EffectiveDate = DateTimeOffset.UtcNow;
        }

        entity.TrainingProfiles = profileTypeIds
            .Select(profileTypeId => new TrainingProfileLinkEntity { TrainingProfileTypeId = profileTypeId })
            .ToList();

        db.Trainings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingLookupResponse?> UpdateAsync(
        int id,
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db
            .Trainings.Include(t => t.TrainingProfiles)
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return null;

        var normalizedRequest = NormalizeRequest(request);
        var profileTypeIds = ResolveMandatoryProfileTypeIds(normalizedRequest);

        normalizedRequest.Adapt(entity);

        var existingProfileLinks = entity.TrainingProfiles.ToList();
        foreach (
            var existing in existingProfileLinks.Where(existing =>
                !profileTypeIds.Contains(existing.TrainingProfileTypeId)
            )
        )
        {
            entity.TrainingProfiles.Remove(existing);
        }

        foreach (
            var profileTypeId in profileTypeIds.Where(profileTypeId =>
                entity.TrainingProfiles.All(existing => existing.TrainingProfileTypeId != profileTypeId)
            )
        )
        {
            entity.TrainingProfiles.Add(new TrainingProfileLinkEntity { TrainingProfileTypeId = profileTypeId });
        }

        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    public async Task<TrainingLookupResponse?> MoveOrderAsync(
        int id,
        int newOrder,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var trainings = await db.Trainings.OrderBy(t => t.Order).ThenBy(t => t.Id).ToListAsync(cancellationToken);

        if (trainings.Count == 0)
            return null;

        var currentIndex = trainings.FindIndex(t => t.Id == id);
        if (currentIndex < 0)
            return null;

        var boundedNewIndex = Math.Clamp(newOrder, 0, trainings.Count - 1);
        if (currentIndex == boundedNewIndex)
            return await GetRequiredByIdAsync(id, cancellationToken);

        var moved = trainings[currentIndex];
        trainings.RemoveAt(currentIndex);
        trainings.Insert(boundedNewIndex, moved);

        var hasChanges = false;
        for (var index = 0; index < trainings.Count; index++)
        {
            var training = trainings[index];
            var normalizedOrder = index;
            if (training.Order == normalizedOrder)
                continue;

            training.Order = normalizedOrder;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    public async Task<TrainingLookupResponse?> ExpireAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Trainings.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return null;

        if (entity.ExpiryDate is null || entity.ExpiryDate > DateTimeOffset.UtcNow)
        {
            entity.ExpiryDate = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    public async Task<TrainingLookupResponse?> UnexpireAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Trainings.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return null;

        if (entity.ExpiryDate is not null)
        {
            entity.ExpiryDate = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    private async Task<TrainingLookupResponse> GetRequiredByIdAsync(int id, CancellationToken cancellationToken) =>
        await BuildResponseQuery(db.Trainings.AsNoTracking().Where(t => t.Id == id)).SingleAsync(cancellationToken);

    private static HashSet<int> ResolveMandatoryProfileTypeIds(TrainingLookupRequest request)
    {
        if (!request.Mandatory || request.MandatoryTrainingProfileIds is null)
        {
            return [];
        }

        return [.. request.MandatoryTrainingProfileIds.Where(id => id > 0)];
    }

    private static IQueryable<TrainingLookupResponse> BuildResponseQuery(IQueryable<TrainingEntity> query) =>
        query.Select(training => new TrainingLookupResponse
        {
            Id = training.Id,
            Code = training.Code,
            Description = training.Description,
            EffectiveDate = training.EffectiveDate,
            ExpiryDate = training.ExpiryDate,
            Mandatory = training.Mandatory,
            ValidityDays = training.ValidityDays,
            AdvanceNoticeDays = training.AdvanceNoticeDays,
            Rotating = training.Rotating,
            TrainingCategoryId = training.TrainingCategoryId,
            TrainingCategoryName = training.TrainingCategory != null ? training.TrainingCategory.Name : null,
            Order = training.Order,
            MandatoryTrainingProfiles = training
                .TrainingProfiles.OrderBy(profile => profile.TrainingProfileType.Code)
                .Select(profile => new TrainingProfileTypeSummary
                {
                    Id = profile.TrainingProfileType.Id,
                    Code = profile.TrainingProfileType.Code,
                    Name = profile.TrainingProfileType.Name,
                })
                .ToList(),
        });

    private static TrainingLookupRequest NormalizeRequest(TrainingLookupRequest request) =>
        request with
        {
            Code = request.Code.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
        };
}
