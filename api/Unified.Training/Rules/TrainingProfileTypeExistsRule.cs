using Microsoft.EntityFrameworkCore;
using Unified.Common.Interceptors;
using TrainingProfileLinkEntity = Unified.Db.Models.Training.TrainingProfile;
using TrainingProfileTypeEntity = Unified.Db.Models.Training.TrainingProfileType;

namespace Unified.Training.Rules;

/// <summary>
/// Validates referenced training profile types exist for pending training-profile link changes.
/// </summary>
public sealed class TrainingProfileTypeExistsRule : ISaveRule
{
    public async Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
    {
        var pendingProfileTypeIds = context
            .ChangeTracker.Entries<TrainingProfileLinkEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity.TrainingProfileTypeId)
            .Where(profileTypeId => profileTypeId > 0)
            .Distinct()
            .ToList();

        if (pendingProfileTypeIds.Count == 0)
        {
            return;
        }

        var existingProfileTypeIds = await context
            .Set<TrainingProfileTypeEntity>()
            .AsNoTracking()
            .Where(profileType => pendingProfileTypeIds.Contains(profileType.Id))
            .Select(profileType => profileType.Id)
            .ToListAsync(cancellationToken);

        var missingProfileTypeIds = pendingProfileTypeIds.Except(existingProfileTypeIds).OrderBy(id => id).ToList();

        if (missingProfileTypeIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Training profile type id(s) not found: {string.Join(", ", missingProfileTypeIds)}."
            );
        }
    }
}
