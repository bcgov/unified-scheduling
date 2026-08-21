using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentDefinitionService(
    ILogger<AssignmentDefinitionService> logger,
    UnifiedDbContext db,
    TimeProvider timeProvider
) : IAssignmentDefinitionService
{
    public async Task<IReadOnlyCollection<AssignmentDefinitionResponse>> GetAssignmentDefinitionsAsync(
        int? locationId = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = IncludeGraph(db.AssignmentDefinitions.AsNoTracking());

        if (locationId is int id)
            query = query.Where(definition => definition.LocationId == id);

        var definitions = await query.OrderBy(definition => definition.Name).ToListAsync(cancellationToken);

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
            NormalizedName = NormalizeNameForComparison(request.Name),
            Description = NormalizeOptionalText(request.Description),
            CategoryId = request.CategoryId,
            SubCategoryId = request.SubCategoryId,
            Color = request.Color?.Trim(),
            DefaultStartTime = ParseTime(request.DefaultStartTime),
            DefaultEndTime = ParseTime(request.DefaultEndTime),
            DefaultCapacity = request.DefaultCapacity,
            EffectiveDateUtc = NormalizeUtcBusinessDate(request.EffectiveDateUtc),
            ExpiryDateUtc = request.ExpiryDateUtc.HasValue
                ? NormalizeUtcBusinessDate(request.ExpiryDateUtc.Value)
                : null,
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
        var definition = await db.AssignmentDefinitions.SingleOrDefaultAsync(
            definition => definition.Id == id,
            cancellationToken
        );
        if (definition is null)
            return null;

        await ValidateRequestAsync(request, id, cancellationToken);

        definition.LocationId = request.LocationId;
        definition.Name = NormalizeNameForStorage(request.Name);
        definition.NormalizedName = NormalizeNameForComparison(request.Name);
        definition.Description = NormalizeOptionalText(request.Description);
        definition.CategoryId = request.CategoryId;
        definition.SubCategoryId = request.SubCategoryId;
        definition.Color = request.Color?.Trim();
        definition.DefaultStartTime = ParseTime(request.DefaultStartTime);
        definition.DefaultEndTime = ParseTime(request.DefaultEndTime);
        definition.DefaultCapacity = request.DefaultCapacity;
        definition.EffectiveDateUtc = NormalizeUtcBusinessDate(request.EffectiveDateUtc);
        definition.ExpiryDateUtc = request.ExpiryDateUtc.HasValue
            ? NormalizeUtcBusinessDate(request.ExpiryDateUtc.Value)
            : null;
        definition.UpdatedOn = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated assignment definition {AssignmentDefinitionId}.", definition.Id);
        return (await GetAssignmentDefinitionByIdAsync(definition.Id, cancellationToken))!;
    }

    private async Task ValidateRequestAsync(
        AssignmentDefinitionRequest request,
        int? currentId,
        CancellationToken cancellationToken
    )
    {
        var name = NormalizeNameForComparison(request.Name);
        if (
            await db.AssignmentDefinitions.AnyAsync(
                definition =>
                    definition.LocationId == request.LocationId
                    && definition.NormalizedName == name
                    && (!currentId.HasValue || definition.Id != currentId.Value),
                cancellationToken
            )
        )
            throw new InvalidOperationException($"Assignment definition name {request.Name.Trim()} already exists.");
        if (!await db.Locations.AnyAsync(location => location.Id == request.LocationId, cancellationToken))
            throw new InvalidOperationException("Location does not exist.");
        if (
            !await db.StatCategories.AnyAsync(
                category => category.Id == request.CategoryId && !category.IsArchived,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Category does not exist or is archived.");
        if (
            !await db.SubCategories.AnyAsync(
                subCategory => subCategory.Id == request.SubCategoryId && subCategory.CategoryId == request.CategoryId,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Subcategory does not exist or does not belong to the category.");
    }

    private static IQueryable<AssignmentDefinition> IncludeGraph(IQueryable<AssignmentDefinition> query) =>
        query.Include(definition => definition.Category).Include(definition => definition.SubCategory);

    private static AssignmentDefinitionResponse MapToResponse(AssignmentDefinition definition) =>
        new()
        {
            Id = definition.Id,
            LocationId = definition.LocationId,
            Name = definition.Name,
            Description = definition.Description,
            CategoryId = definition.CategoryId,
            CategoryName = definition.Category.Name,
            SubCategoryId = definition.SubCategoryId,
            SubCategoryName = definition.SubCategory.Name,
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

    private static DateTimeOffset NormalizeUtcBusinessDate(DateTimeOffset value)
    {
        var date = DateOnly.FromDateTime(value.UtcDateTime);
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}
