using Microsoft.EntityFrameworkCore;
using Unified.Common.Helpers.Extensions;
using Unified.Db;
using Unified.Db.Models.Lookup;
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
        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        var positions = await db
            .UserActingPositions.AsNoTracking()
            .Include(x => x.PositionType)
            .Where(x => x.UserId == userId && (x.ExpiryAtUtc == null || x.ExpiryAtUtc > DateTimeOffset.UtcNow))
            .OrderByDescending(x => x.StartAtUtc)
            .ThenBy(x => x.PositionTypeId)
            .ToListAsync(cancellationToken);

        var timezoneId = user.HomeLocation?.Timezone;
        return positions.Select(p => MapResponse(p, p.Timezone ?? timezoneId)).ToList();
    }

    public async Task<ActingPositionResponseDto> CreateAsync(
        Guid userId,
        ActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        var positionType = await GetPositionTypeOrThrowAsync(request.PositionTypeCode, cancellationToken);

        var timezoneId = request.Timezone ?? user.HomeLocation?.Timezone;

        var effectiveDateUtc = DateTimeOffset.Parse(request.StartDateTime).ToUniversalTime();
        var endDateUtc = DateTimeOffset.Parse(request.EndDateTime).ToUniversalTime();

        var actingPosition = new UserActingPosition
        {
            UserId = userId,
            PositionTypeId = positionType.Id,
            StartAtUtc = effectiveDateUtc,
            EndAtUtc = endDateUtc,
            Timezone = timezoneId,
            Comment = request.Comment?.Trim(),
        };

        db.UserActingPositions.Add(actingPosition);
        await db.SaveChangesAsync(cancellationToken);

        actingPosition.PositionType = positionType;
        return MapResponse(actingPosition, timezoneId);
    }

    public async Task<ActingPositionResponseDto> UpdateAsync(
        Guid userId,
        int actingPositionId,
        ActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        var actingPosition = await db
            .UserActingPositions.Include(x => x.PositionType)
            .SingleOrDefaultAsync(x => x.Id == actingPositionId && x.UserId == userId, cancellationToken);

        if (actingPosition is null)
        {
            throw new KeyNotFoundException($"Acting position {actingPositionId} not found for user {userId}.");
        }

        var positionType = await GetPositionTypeOrThrowAsync(request.PositionTypeCode, cancellationToken);

        var timezoneId = request.Timezone ?? user.HomeLocation?.Timezone;

        var effectiveDateUtc = DateTimeOffset.Parse(request.StartDateTime).ToUniversalTime();
        var endDateUtc = DateTimeOffset.Parse(request.EndDateTime).ToUniversalTime();

        actingPosition.PositionTypeId = positionType.Id;
        actingPosition.StartAtUtc = effectiveDateUtc;
        actingPosition.EndAtUtc = endDateUtc;
        actingPosition.Timezone = timezoneId;
        actingPosition.Comment = request.Comment?.Trim();
        actingPosition.PositionType = positionType;

        await db.SaveChangesAsync(cancellationToken);

        return MapResponse(actingPosition, timezoneId);
    }

    public async Task<ActingPositionResponseDto> ExpireAsync(
        Guid userId,
        ExpireActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        var actingPosition = await db
            .UserActingPositions.Include(x => x.PositionType)
            .SingleOrDefaultAsync(x => x.Id == request.ActingPositionId && x.UserId == userId, cancellationToken);

        if (actingPosition is null)
        {
            throw new KeyNotFoundException($"Acting position {request.ActingPositionId} not found for user {userId}.");
        }

        actingPosition.ExpiryAtUtc = DateTimeOffset.UtcNow;
        actingPosition.ExpiryReason = request.ExpiryReason.Trim();

        await db.SaveChangesAsync(cancellationToken);

        return MapResponse(actingPosition, actingPosition.Timezone ?? user.HomeLocation?.Timezone);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        return user;
    }

    private async Task<PositionType> GetPositionTypeOrThrowAsync(
        string positionTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        var positionType = await db.PositionTypes.SingleOrDefaultAsync(
            pt => pt.Code == positionTypeCode,
            cancellationToken
        );
        if (positionType is null)
        {
            throw new KeyNotFoundException($"PositionType '{positionTypeCode}' not found.");
        }

        return positionType;
    }

    private static ActingPositionResponseDto MapResponse(UserActingPosition position, string? timezoneId) =>
        new()
        {
            Id = position.Id,
            UserId = position.UserId,
            PositionTypeCode = position.PositionType?.Code ?? string.Empty,
            PositionTypeDescription = position.PositionType?.Description ?? string.Empty,
            StartAtUtc = position.StartAtUtc,
            EndAtUtc = position.EndAtUtc,
            ExpiryAtUtc = position.ExpiryAtUtc,
            ExpiryReason = position.ExpiryReason,
            Comment = position.Comment,
            Timezone = timezoneId,
        };
}
