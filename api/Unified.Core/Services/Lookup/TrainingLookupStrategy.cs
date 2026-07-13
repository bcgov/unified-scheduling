using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Core.Services.Lookup;

public sealed class TrainingLookupStrategy(UnifiedDbContext db) : ITrainingLookupStrategy
{
    public LookupCodeTypes CodeType => LookupCodeTypes.Trainings;

    public async Task<IReadOnlyCollection<TrainingLookupResponse>> GetAllTrainingsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await BuildResponseQuery()
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .Trainings.AsNoTracking()
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Code)
            .Select(t => new LookupCodeResponse
            {
                Code = t.Code,
                Description = t.Description,
                EffectiveDate = t.EffectiveDate,
                ExpiryDate = t.ExpiryDate,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainingLookupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await BuildResponseQuery()
            .Where(t => t.Id == id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TrainingLookupResponse> CreateAsync(
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCategoryExistsAsync(request.TrainingCategoryId, cancellationToken);

        var normalizedRequest = NormalizeRequest(request);

        var entity = new TrainingEntity
        {
            Code = normalizedRequest.Code,
            Description = normalizedRequest.Description,
            Mandatory = normalizedRequest.Mandatory,
            ValidityDays = normalizedRequest.ValidityDays,
            AdvanceNoticeDays = normalizedRequest.AdvanceNoticeDays,
            Rotating = normalizedRequest.Rotating,
            TrainingCategoryId = normalizedRequest.TrainingCategoryId,
            Order = normalizedRequest.Order,
        };

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
        var entity = await db.Trainings.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return null;

        await EnsureCategoryExistsAsync(request.TrainingCategoryId, cancellationToken);

        var normalizedRequest = NormalizeRequest(request);

        entity.Code = normalizedRequest.Code;
        entity.Description = normalizedRequest.Description;
        entity.Mandatory = normalizedRequest.Mandatory;
        entity.ValidityDays = normalizedRequest.ValidityDays;
        entity.AdvanceNoticeDays = normalizedRequest.AdvanceNoticeDays;
        entity.Rotating = normalizedRequest.Rotating;
        entity.TrainingCategoryId = normalizedRequest.TrainingCategoryId;
        entity.Order = normalizedRequest.Order;

        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    public async Task<TrainingLookupResponse?> MoveOrderAsync(
        int id,
        int newOrder,
        CancellationToken cancellationToken = default
    )
    {
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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var hasChanges = false;
        for (var index = 0; index < trainings.Count; index++)
        {
            var training = trainings[index];
            if (training.Order == index)
                continue;

            training.Order = index;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

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

    private IQueryable<TrainingLookupResponse> BuildResponseQuery()
    {
        return db.Trainings.AsNoTracking().Select(t => new TrainingLookupResponse
        {
            Id = t.Id,
            Code = t.Code,
            Description = t.Description,
            EffectiveDate = t.EffectiveDate,
            ExpiryDate = t.ExpiryDate,
            Mandatory = t.Mandatory,
            ValidityDays = t.ValidityDays,
            AdvanceNoticeDays = t.AdvanceNoticeDays,
            Rotating = t.Rotating,
            TrainingCategoryId = t.TrainingCategoryId,
            TrainingCategoryName = t.TrainingCategory != null ? t.TrainingCategory.Name : null,
            Order = t.Order,
            CreatedOn = t.CreatedOn,
            UpdatedOn = t.UpdatedOn,
        });
    }

    private async Task<TrainingLookupResponse> GetRequiredByIdAsync(int id, CancellationToken cancellationToken) =>
        await BuildResponseQuery().Where(t => t.Id == id).SingleAsync(cancellationToken);

    private static TrainingLookupRequest NormalizeRequest(TrainingLookupRequest request) =>
        request with
        {
            Code = request.Code.Trim(),
            Description = request.Description.Trim(),
        };
}