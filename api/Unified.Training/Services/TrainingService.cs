using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Training.Models;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Training.Services;

public sealed class TrainingService(UnifiedDbContext db) : ITrainingService
{
    public async Task<IReadOnlyCollection<TrainingResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db
            .Trainings.AsNoTracking()
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Code)
            .ProjectToType<TrainingResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainingResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db
            .Trainings.AsNoTracking()
            .Where(t => t.Id == id)
            .ProjectToType<TrainingResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TrainingResponse> CreateAsync(
        TrainingRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCategoryExistsAsync(request.TrainingCategoryId, cancellationToken);

        var normalizedRequest = NormalizeRequest(request);

        var entity = normalizedRequest.Adapt<TrainingEntity>();

        db.Trainings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingResponse?> UpdateAsync(
        int id,
        TrainingRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.Trainings.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return null;

        await EnsureCategoryExistsAsync(request.TrainingCategoryId, cancellationToken);

        var normalizedRequest = NormalizeRequest(request);

        normalizedRequest.Adapt(entity);

        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    public async Task<TrainingResponse?> MoveOrderAsync(
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

    private async Task<TrainingResponse> GetRequiredByIdAsync(int id, CancellationToken cancellationToken) =>
        await db
            .Trainings.AsNoTracking()
            .Where(t => t.Id == id)
            .ProjectToType<TrainingResponse>()
            .SingleAsync(cancellationToken);

    private static TrainingRequest NormalizeRequest(TrainingRequest request) =>
        request with
        {
            Code = request.Code.Trim(),
            Description = request.Description.Trim(),
        };
}
