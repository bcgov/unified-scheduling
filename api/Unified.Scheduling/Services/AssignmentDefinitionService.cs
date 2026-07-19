using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentDefinitionService(ILogger<AssignmentDefinitionService> logger, UnifiedDbContext db)
    : IAssignmentDefinitionService
{
    public async Task<IReadOnlyCollection<AssignmentDefinitionResponse>> GetAssignmentDefinitionsAsync(
        int? locationId = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = IncludeGraph(db.AssignmentDefinitions.AsNoTracking());

        if (locationId is int id)
            query = query.Where(definition => definition.LocationId == id);

        var definitions = await query
            .OrderBy(definition => definition.Name)
            .ToListAsync(cancellationToken);

        return definitions.Select(MapToResponse).ToList();
    }

    public async Task<AssignmentDefinitionResponse?> GetAssignmentDefinitionByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var definition = await IncludeGraph(db.AssignmentDefinitions.AsNoTracking())
            .SingleOrDefaultAsync(definition => definition.Id == id, cancellationToken);
        return definition is null ? null : MapToResponse(definition);
    }

    public async Task<AssignmentDefinitionResponse> CreateAssignmentDefinitionAsync(
        AssignmentDefinitionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateRequestAsync(request, null, cancellationToken);
        var definition = new AssignmentDefinition
        {
            LocationId = request.LocationId,
            Name = NormalizeNameForStorage(request.Name),
            Description = NormalizeOptionalText(request.Description),
            AssignmentCategoryTypeId = request.AssignmentCategoryTypeId,
            AssignmentSubCategoryTypeId = request.AssignmentSubCategoryTypeId,
            Color = request.Color?.Trim(),
            DefaultStartTime = ParseTime(request.DefaultStartTime),
            DefaultEndTime = ParseTime(request.DefaultEndTime),
            DefaultCapacity = request.DefaultCapacity,
            EffectiveDateUtc = request.EffectiveDateUtc,
            ExpiryDateUtc = request.ExpiryDateUtc,
        };

        db.AssignmentDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created assignment definition {AssignmentDefinitionId}.", definition.Id);
        return (await GetAssignmentDefinitionByIdAsync(definition.Id, cancellationToken))!;
    }

    public async Task<AssignmentDefinitionResponse?> UpdateAssignmentDefinitionAsync(
        int id,
        AssignmentDefinitionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var definition = await db.AssignmentDefinitions.SingleOrDefaultAsync(definition => definition.Id == id, cancellationToken);
        if (definition is null)
            return null;

        await ValidateRequestAsync(request, id, cancellationToken);

        definition.LocationId = request.LocationId;
        definition.Name = NormalizeNameForStorage(request.Name);
        definition.Description = NormalizeOptionalText(request.Description);
        definition.AssignmentCategoryTypeId = request.AssignmentCategoryTypeId;
        definition.AssignmentSubCategoryTypeId = request.AssignmentSubCategoryTypeId;
        definition.Color = request.Color?.Trim();
        definition.DefaultStartTime = ParseTime(request.DefaultStartTime);
        definition.DefaultEndTime = ParseTime(request.DefaultEndTime);
        definition.DefaultCapacity = request.DefaultCapacity;
        definition.EffectiveDateUtc = request.EffectiveDateUtc;
        definition.ExpiryDateUtc = request.ExpiryDateUtc;
        definition.UpdatedOn = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return (await GetAssignmentDefinitionByIdAsync(definition.Id, cancellationToken))!;
    }

    private async Task ValidateRequestAsync(
        AssignmentDefinitionRequest request,
        int? currentId,
        CancellationToken cancellationToken
    )
    {
        var now = DateTimeOffset.UtcNow;
        var name = NormalizeNameForComparison(request.Name);
        if (await db.AssignmentDefinitions.AnyAsync(
                definition =>
                    definition.LocationId == request.LocationId
                    && definition.Name.ToUpper() == name
                    && (!currentId.HasValue || definition.Id != currentId.Value),
                cancellationToken))
            throw new InvalidOperationException($"Assignment definition name {request.Name.Trim()} already exists.");
        if (!await db.Locations.AnyAsync(location => location.Id == request.LocationId, cancellationToken))
            throw new InvalidOperationException("Location does not exist.");
        if (!await IsActiveCodeAsync(db.AssignmentCategoryTypes, request.AssignmentCategoryTypeId, now, cancellationToken))
            throw new InvalidOperationException("Assignment category type is not active.");
        if (!await IsActiveCodeAsync(db.AssignmentSubCategoryTypes, request.AssignmentSubCategoryTypeId, now, cancellationToken))
            throw new InvalidOperationException("Assignment subcategory type is not active.");
    }

    private static IQueryable<AssignmentDefinition> IncludeGraph(IQueryable<AssignmentDefinition> query) =>
        query
            .Include(definition => definition.AssignmentCategoryType)
            .Include(definition => definition.AssignmentSubCategoryType);

    private static Task<bool> IsActiveCodeAsync<T>(
        DbSet<T> set,
        int id,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
        where T : Unified.Db.Models.Abstract.BaseCodeTypeEntity =>
        set.AnyAsync(
            code =>
                EF.Property<int>(code, "Id") == id
                && code.EffectiveDate <= now
                && (!code.ExpiryDate.HasValue || code.ExpiryDate > now),
            cancellationToken
        );

    private static AssignmentDefinitionResponse MapToResponse(AssignmentDefinition definition) =>
        new()
        {
            Id = definition.Id,
            LocationId = definition.LocationId,
            Name = definition.Name,
            Description = definition.Description,
            AssignmentCategoryTypeId = definition.AssignmentCategoryTypeId,
            AssignmentCategoryTypeDescription = definition.AssignmentCategoryType.Description,
            AssignmentSubCategoryTypeId = definition.AssignmentSubCategoryTypeId,
            AssignmentSubCategoryTypeDescription = definition.AssignmentSubCategoryType.Description,
            Color = definition.Color,
            DefaultStartTime = definition.DefaultStartTime?.ToString("HH:mm:ss"),
            DefaultEndTime = definition.DefaultEndTime?.ToString("HH:mm:ss"),
            DefaultCapacity = definition.DefaultCapacity,
            EffectiveDateUtc = definition.EffectiveDateUtc,
            ExpiryDateUtc = definition.ExpiryDateUtc,
        };

    private static string NormalizeNameForStorage(string name) => name.Trim();

    private static string NormalizeNameForComparison(string name) => name.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static TimeOnly? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : TimeOnly.Parse(value);
}
