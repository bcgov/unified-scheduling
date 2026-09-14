using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Training.Models;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Training.Services.Lookup;

public sealed class TrainingLookupStrategy(UnifiedDbContext db) : ITrainingLookupStrategy
{
    private sealed record MandatoryProfileLinkRow(int TrainingId, int TrainingProfileId, string TrainingProfileCode);

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

        var trainings = await BuildLookupBaseQuery(query)
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Code)
            .ToListAsync(cancellationToken);

        return await PopulateMandatoryTrainingProfilesAsync(trainings, cancellationToken);
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
        var training = await BuildLookupBaseQuery(db.Trainings.AsNoTracking().Where(t => t.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        if (training is null)
        {
            return null;
        }

        var populated = await PopulateMandatoryTrainingProfilesAsync([training], cancellationToken);
        return populated.First();
    }

    public async Task<TrainingLookupResponse> CreateAsync(
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCategoryExistsAsync(request.TrainingCategoryId, cancellationToken);

        var normalizedRequest = NormalizeRequest(request);
        await EnsureMandatoryTrainingProfilesExistAsync(normalizedRequest.MandatoryTrainingProfileIds, cancellationToken);

        var entity = normalizedRequest.Adapt<TrainingEntity>();
        if (entity.EffectiveDate == default)
        {
            entity.EffectiveDate = DateTimeOffset.UtcNow;
        }

        db.Trainings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        await SyncMandatoryTrainingProfilesAsync(entity.Id, normalizedRequest, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingLookupResponse?> UpdateAsync(
        int id,
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.Trainings.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return null;

        await EnsureCategoryExistsAsync(request.TrainingCategoryId, cancellationToken);

        var normalizedRequest = NormalizeRequest(request);
        await EnsureMandatoryTrainingProfilesExistAsync(normalizedRequest.MandatoryTrainingProfileIds, cancellationToken);
        normalizedRequest.Adapt(entity);

        await SyncMandatoryTrainingProfilesAsync(entity.Id, normalizedRequest, cancellationToken);

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
        var entity = await db.Trainings.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
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
        var entity = await db.Trainings.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return null;

        if (entity.ExpiryDate is not null)
        {
            entity.ExpiryDate = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    private async Task EnsureCategoryExistsAsync(int? trainingCategoryId, CancellationToken cancellationToken)
    {
        if (trainingCategoryId is null)
            return;

        var categoryExists = await db
            .TrainingCategories.AsNoTracking()
            .AnyAsync(tc => tc.Id == trainingCategoryId, cancellationToken);

        if (!categoryExists)
            throw new InvalidOperationException("Training category was not found.");
    }

    private async Task<TrainingLookupResponse> GetRequiredByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    private static IQueryable<TrainingLookupResponse> BuildLookupBaseQuery(IQueryable<TrainingEntity> query)
    {
        return query.Select(training => new TrainingLookupResponse
        {
            Id = training.Id,
            Code = training.Code,
            Description = training.Description,
            Mandatory = training.Mandatory,
            ValidityDays = training.ValidityDays,
            AdvanceNoticeDays = training.AdvanceNoticeDays,
            Rotating = training.Rotating,
            TrainingCategoryId = training.TrainingCategoryId,
            TrainingCategoryName = training.TrainingCategory != null ? training.TrainingCategory.Name : null,
            Order = training.Order,
            EffectiveDate = training.EffectiveDate,
            ExpiryDate = training.ExpiryDate,
            CreatedOn = training.CreatedOn,
            UpdatedOn = training.UpdatedOn,
        });
    }

    private async Task<IReadOnlyCollection<TrainingLookupResponse>> PopulateMandatoryTrainingProfilesAsync(
        IReadOnlyCollection<TrainingLookupResponse> trainings,
        CancellationToken cancellationToken
    )
    {
        if (trainings.Count == 0)
        {
            return trainings;
        }

        var trainingIds = trainings.Select(training => training.Id).ToArray();

        var mandatoryProfileLinks = await db
            .TrainingMandatoryTrainingProfiles.AsNoTracking()
            .Where(link => trainingIds.Contains(link.TrainingId))
            .Select(link => new MandatoryProfileLinkRow(link.TrainingId, link.TrainingProfileId, link.TrainingProfile.Code))
            .ToListAsync(cancellationToken);

        var groupedLinks = mandatoryProfileLinks
            .GroupBy(link => link.TrainingId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(link => link.TrainingProfileCode).ToArray()
            );

        return trainings
            .Select(training =>
            {
                if (!groupedLinks.TryGetValue(training.Id, out var links))
                {
                    return training with
                    {
                        MandatoryTrainingProfileIds = [],
                        MandatoryTrainingProfileCodes = [],
                    };
                }

                return training with
                {
                    MandatoryTrainingProfileIds = links.Select(link => link.TrainingProfileId).ToArray(),
                    MandatoryTrainingProfileCodes = links.Select(link => link.TrainingProfileCode).ToArray(),
                };
            })
            .ToArray();
    }

    private async Task EnsureMandatoryTrainingProfilesExistAsync(
        IReadOnlyCollection<int>? mandatoryTrainingProfileIds,
        CancellationToken cancellationToken
    )
    {
        if (mandatoryTrainingProfileIds is not { Count: > 0 })
        {
            return;
        }

        var profileIds = mandatoryTrainingProfileIds.Distinct().ToArray();
        var existingCount = await db
            .TrainingProfiles.AsNoTracking()
            .CountAsync(profile => profileIds.Contains(profile.Id), cancellationToken);

        if (existingCount != profileIds.Length)
        {
            throw new InvalidOperationException("One or more mandatory training profiles were not found.");
        }
    }

    private async Task SyncMandatoryTrainingProfilesAsync(
        int trainingId,
        TrainingLookupRequest request,
        CancellationToken cancellationToken
    )
    {
        var existingLinks = await db
            .TrainingMandatoryTrainingProfiles.Where(link => link.TrainingId == trainingId)
            .ToListAsync(cancellationToken);

        var profileIds = request.Mandatory
            ? request.MandatoryTrainingProfileIds?.Distinct().ToArray() ?? []
            : [];

        var requestedIds = profileIds.ToHashSet();
        var existingIds = existingLinks.Select(link => link.TrainingProfileId).ToHashSet();

        var linksToRemove = existingLinks.Where(link => !requestedIds.Contains(link.TrainingProfileId)).ToArray();
        if (linksToRemove.Length > 0)
        {
            db.TrainingMandatoryTrainingProfiles.RemoveRange(linksToRemove);
        }

        var linksToAdd = requestedIds
            .Where(profileId => !existingIds.Contains(profileId))
            .Select(profileId => new TrainingMandatoryTrainingProfile
            {
                TrainingId = trainingId,
                TrainingProfileId = profileId,
            })
            .ToArray();

        if (linksToAdd.Length == 0)
        {
            return;
        }

        db.TrainingMandatoryTrainingProfiles.AddRange(linksToAdd);
    }

    private static TrainingLookupRequest NormalizeRequest(TrainingLookupRequest request) =>
        request with
        {
            Code = request.Code.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            MandatoryTrainingProfileIds = request
                .MandatoryTrainingProfileIds?.Where(profileId => profileId > 0)
                .Distinct()
                .ToArray(),
        };
}
