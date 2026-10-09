using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Training.Models;

namespace Unified.Training.Services;

public sealed class TrainingProfileTypeService(UnifiedDbContext db) : ITrainingProfileTypeService
{
    public async Task<IReadOnlyCollection<TrainingProfileTypeResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .TrainingProfileTypes.AsNoTracking()
            .OrderBy(profile => profile.Name)
            .ThenBy(profile => profile.Code)
            .Select(profile => new TrainingProfileTypeResponse
            {
                Id = profile.Id,
                Code = profile.Code,
                Name = profile.Name,
            })
            .ToListAsync(cancellationToken);
    }
}
