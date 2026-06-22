using Microsoft.EntityFrameworkCore;
using Unified.Common.Helpers.Extensions;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class ActingPositionService(UnifiedDbContext db) : IActingPositionService
{
    public async Task<IReadOnlyCollection<ActingPositionResponseDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await db
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        var positions = await db
            .UserActingPositions.AsNoTracking()
            .Include(x => x.PositionType)
            .Where(x => x.UserId == userId && x.ExpiryDate == null)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenBy(x => x.PositionTypeId)
            .ToListAsync(cancellationToken);

        var timezoneId = user.HomeLocation?.Timezone;
        return positions.Select(p => MapResponse(p, timezoneId)).ToList();
    }

    public async Task<ActingPositionResponseDto> CreateAsync(
        Guid userId,
        ActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await db
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        var positionType = await db.PositionTypes.SingleOrDefaultAsync(
            pt => pt.Code == request.PositionTypeCode,
            cancellationToken
        );
        if (positionType is null)
        {
            throw new KeyNotFoundException($"PositionType '{request.PositionTypeCode}' not found.");
        }

        var effectiveDateUtc = DateTimeOffsetExtensions.FromDateStringToStartOfDayInTimeZone(
            request.EffectiveDate,
            user.HomeLocation?.Timezone
        );
        DateTimeOffset? expiryDateUtc = !string.IsNullOrEmpty(request.ExpiryDate)
            ? DateTimeOffsetExtensions.FromDateStringToEndOfDayInTimeZone(
                request.ExpiryDate,
                user.HomeLocation?.Timezone
            )
            : null;

        var actingPosition = new UserActingPosition
        {
            UserId = userId,
            PositionTypeId = positionType.Id,
            EffectiveDate = effectiveDateUtc,
            ExpiryDate = expiryDateUtc,
            Comment = request.Comment?.Trim(),
        };

        db.UserActingPositions.Add(actingPosition);
        await db.SaveChangesAsync(cancellationToken);

        actingPosition.PositionType = positionType;
        return MapResponse(actingPosition, user.HomeLocation?.Timezone);
    }

    public async Task<ActingPositionResponseDto> UpdateAsync(
        Guid userId,
        int actingPositionId,
        ActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await db
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        var actingPosition = await db
            .UserActingPositions.Include(x => x.PositionType)
            .SingleOrDefaultAsync(x => x.Id == actingPositionId && x.UserId == userId, cancellationToken);

        if (actingPosition is null)
        {
            throw new KeyNotFoundException($"Acting position {actingPositionId} not found for user {userId}.");
        }

        var positionType = await db.PositionTypes.SingleOrDefaultAsync(
            pt => pt.Code == request.PositionTypeCode,
            cancellationToken
        );
        if (positionType is null)
        {
            throw new KeyNotFoundException($"PositionType '{request.PositionTypeCode}' not found.");
        }

        var effectiveDateUtc = DateTimeOffsetExtensions.FromDateStringToStartOfDayInTimeZone(
            request.EffectiveDate,
            user.HomeLocation?.Timezone
        );
        DateTimeOffset? expiryDateUtc = !string.IsNullOrEmpty(request.ExpiryDate)
            ? DateTimeOffsetExtensions.FromDateStringToEndOfDayInTimeZone(
                request.ExpiryDate,
                user.HomeLocation?.Timezone
            )
            : null;

        actingPosition.PositionTypeId = positionType.Id;
        actingPosition.EffectiveDate = effectiveDateUtc;
        actingPosition.ExpiryDate = expiryDateUtc;
        actingPosition.Comment = request.Comment?.Trim();
        actingPosition.PositionType = positionType;

        await db.SaveChangesAsync(cancellationToken);

        return MapResponse(actingPosition, user.HomeLocation?.Timezone);
    }

    public async Task<ActingPositionResponseDto> ExpireAsync(
        Guid userId,
        ExpireActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await db
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        var actingPosition = await db
            .UserActingPositions.Include(x => x.PositionType)
            .SingleOrDefaultAsync(x => x.Id == request.ActingPositionId && x.UserId == userId, cancellationToken);

        if (actingPosition is null)
        {
            throw new KeyNotFoundException($"Acting position {request.ActingPositionId} not found for user {userId}.");
        }

        actingPosition.ExpiryDate = DateTimeOffset.UtcNow;
        actingPosition.ExpiryReason = request.ExpiryReason.Trim();

        await db.SaveChangesAsync(cancellationToken);

        return MapResponse(actingPosition, user.HomeLocation?.Timezone);
    }

    private static ActingPositionResponseDto MapResponse(UserActingPosition position, string? timezoneId) =>
        new()
        {
            Id = position.Id,
            UserId = position.UserId,
            PositionTypeCode = position.PositionType?.Code ?? string.Empty,
            PositionTypeDescription = position.PositionType?.Description ?? string.Empty,
            EffectiveDate = position.EffectiveDate.ToTimeZone(timezoneId),
            ExpiryDate = position.ExpiryDate?.ToTimeZone(timezoneId),
            ExpiryReason = position.ExpiryReason,
            Comment = position.Comment,
        };
}
