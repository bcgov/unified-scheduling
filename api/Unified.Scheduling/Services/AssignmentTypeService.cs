using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Db.Models.Lookup;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentTypeService(ILogger<AssignmentTypeService> logger, UnifiedDbContext db)
    : IAssignmentTypeService
{
    public async Task<IReadOnlyCollection<AssignmentTypeResponse>> GetAssignmentTypesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var assignmentTypes = await db
            .AssignmentTypes.AsNoTracking()
            .OrderBy(type => type.Code)
            .ToListAsync(cancellationToken);

        return assignmentTypes.Select(MapToResponse).ToList();
    }

    public async Task<AssignmentTypeResponse?> GetAssignmentTypeByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var assignmentType = await db.AssignmentTypes.AsNoTracking().SingleOrDefaultAsync(type => type.Id == id, cancellationToken);
        return assignmentType is null ? null : MapToResponse(assignmentType);
    }

    public async Task<AssignmentTypeResponse?> GetAssignmentTypeByCodeAsync(
        string code,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedCode = NormalizeCode(code);
        var assignmentType = await db
            .AssignmentTypes.AsNoTracking()
            .SingleOrDefaultAsync(type => type.Code == normalizedCode, cancellationToken);
        return assignmentType is null ? null : MapToResponse(assignmentType);
    }

    public async Task<AssignmentTypeResponse> CreateAssignmentTypeAsync(
        AssignmentTypeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var code = NormalizeCode(request.Code);
        logger.LogInformation("Creating assignment type {AssignmentTypeCode}.", code);
        await EnsureCodeIsUniqueAsync(code, null, cancellationToken);

        var assignmentType = new AssignmentType
        {
            Code = code,
            Description = request.Description.Trim(),
            EffectiveDate = request.EffectiveDate,
            ExpiryDate = request.ExpiryDate,
        };

        db.AssignmentTypes.Add(assignmentType);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created assignment type {AssignmentTypeId}.", assignmentType.Id);
        return MapToResponse(assignmentType);
    }

    public async Task<AssignmentTypeResponse?> UpdateAssignmentTypeAsync(
        int id,
        AssignmentTypeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Updating assignment type {AssignmentTypeId}.", id);

        var assignmentType = await db.AssignmentTypes.SingleOrDefaultAsync(type => type.Id == id, cancellationToken);
        if (assignmentType is null)
            return null;

        if (IsExpired(assignmentType))
        {
            logger.LogInformation(
                "Blocked update for expired assignment type {AssignmentTypeId}.",
                assignmentType.Id
            );
            throw new InvalidOperationException("Expired assignment types cannot be updated.");
        }

        var code = NormalizeCode(request.Code);
        await EnsureCodeIsUniqueAsync(code, id, cancellationToken);

        assignmentType.Code = code;
        assignmentType.Description = request.Description.Trim();
        assignmentType.EffectiveDate = request.EffectiveDate;
        assignmentType.ExpiryDate = request.ExpiryDate;
        assignmentType.UpdatedOn = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated assignment type {AssignmentTypeId}.", assignmentType.Id);
        return MapToResponse(assignmentType);
    }

    public async Task<AssignmentTypeResponse?> ExpireAssignmentTypeAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Expiring assignment type {AssignmentTypeId}.", id);

        var assignmentType = await db.AssignmentTypes.SingleOrDefaultAsync(type => type.Id == id, cancellationToken);
        if (assignmentType is null)
            return null;

        assignmentType.ExpiryDate = DateTimeOffset.UtcNow;
        assignmentType.UpdatedOn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Expired assignment type {AssignmentTypeId}.", assignmentType.Id);
        return MapToResponse(assignmentType);
    }

    private async Task EnsureCodeIsUniqueAsync(string code, int? currentId, CancellationToken cancellationToken)
    {
        var codeExists = await db.AssignmentTypes.AnyAsync(
            type => type.Code == code && (!currentId.HasValue || type.Id != currentId.Value),
            cancellationToken
        );

        if (codeExists)
            throw new InvalidOperationException($"Assignment type code {code} already exists.");
    }

    private static bool IsExpired(AssignmentType assignmentType) =>
        assignmentType.ExpiryDate.HasValue && assignmentType.ExpiryDate.Value <= DateTimeOffset.UtcNow;

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static AssignmentTypeResponse MapToResponse(AssignmentType assignmentType) =>
        new()
        {
            Id = assignmentType.Id,
            Code = assignmentType.Code,
            Description = assignmentType.Description,
            EffectiveDate = assignmentType.EffectiveDate,
            ExpiryDate = assignmentType.ExpiryDate,
        };
}
