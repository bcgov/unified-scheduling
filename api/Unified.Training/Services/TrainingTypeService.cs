using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using TrainingEntity = Unified.Db.Models.Training.Training;
using Unified.Training.Models;

namespace Unified.Training.Services;

public sealed class TrainingsService(UnifiedDbContext db) : ITrainingsService
{
    public async Task<IReadOnlyCollection<TrainingResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db
            .Trainings.AsNoTracking()
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Code)
            .Select(ProjectToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainingResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db
            .Trainings.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(ProjectToResponse())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TrainingResponse> CreateAsync(
        TrainingRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCategoryExistsAsync(request.TrainingCategoryId, cancellationToken);

        var normalizedCode = request.Code.Trim();
        await EnsureCodeUniqueAsync(normalizedCode, excludeId: null, cancellationToken);

        var entity = new TrainingEntity
        {
            Code = normalizedCode,
            Description = request.Description.Trim(),
            Mandatory = request.Mandatory,
            ValidityDays = request.ValidityDays,
            AdvanceNoticeDays = request.AdvanceNoticeDays,
            Rotating = request.Rotating,
            TrainingCategoryId = request.TrainingCategoryId,
            Order = request.Order,
        };

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

        var normalizedCode = request.Code.Trim();
        await EnsureCodeUniqueAsync(normalizedCode, id, cancellationToken);

        entity.Code = normalizedCode;
        entity.Description = request.Description.Trim();
        entity.Mandatory = request.Mandatory;
        entity.ValidityDays = request.ValidityDays;
        entity.AdvanceNoticeDays = request.AdvanceNoticeDays;
        entity.Rotating = request.Rotating;
        entity.TrainingCategoryId = request.TrainingCategoryId;
        entity.Order = request.Order;

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
            .Select(ProjectToResponse())
            .SingleAsync(cancellationToken);

    private static Expression<Func<TrainingEntity, TrainingResponse>> ProjectToResponse() => t => new()
    {
        Id = t.Id,
        Code = t.Code,
        Description = t.Description,
        Mandatory = t.Mandatory,
        ValidityDays = t.ValidityDays,
        AdvanceNoticeDays = t.AdvanceNoticeDays,
        Rotating = t.Rotating,
        TrainingCategoryId = t.TrainingCategoryId,
        TrainingCategoryName = t.TrainingCategory != null ? t.TrainingCategory.Name : null,
        Order = t.Order,
        CreatedOn = t.CreatedOn,
        UpdatedOn = t.UpdatedOn,
    };
}
