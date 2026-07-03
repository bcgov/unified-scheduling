using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using TrainingEntity = Unified.Db.Models.Training.Training;
using Unified.Training.Models;

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
        await EnsureCodeUniqueAsync(normalizedRequest.Code, excludeId: null, cancellationToken);

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
        await EnsureCodeUniqueAsync(normalizedRequest.Code, id, cancellationToken);

        normalizedRequest.Adapt(entity);

        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Trainings.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            return false;

        var inUse = await db.UserTrainings.AnyAsync(ut => ut.TrainingId == id, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("Cannot delete a training type that has assigned user training records.");

        db.Trainings.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        return true;
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

    private async Task EnsureCodeUniqueAsync(string code, int? excludeId, CancellationToken cancellationToken)
    {
        var codeUpper = code.ToUpperInvariant();
        var exists = await db.Trainings.AnyAsync(
            t =>
                (excludeId == null || t.Id != excludeId)
                && t.Code.ToUpper() == codeUpper,
            cancellationToken
        );

        if (exists)
            throw new InvalidOperationException("A training type with this code already exists.");
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
