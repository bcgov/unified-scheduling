using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class AwayLocationService(UnifiedDbContext db) : IAwayLocationService
{
    public async Task<IReadOnlyCollection<AwayLocationResponseDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await GetUserOrThrowAsync(userId, cancellationToken);

        var awayLocations = await db
            .UserAwayLocations.AsNoTracking()
            .Include(x => x.Location)
            .Where(x => x.UserId == userId && (x.ExpiryAtUtc == null || x.ExpiryAtUtc > DateTimeOffset.UtcNow))
            .OrderByDescending(x => x.StartAtUtc)
            .ThenBy(x => x.LocationId)
            .ToListAsync(cancellationToken);

        return awayLocations.Select(a => MapResponse(a)).ToList();
    }

    public async Task<AwayLocationResponseDto> CreateAsync(
        Guid userId,
        AwayLocationRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var location = await GetLocationOrThrowAsync(request.LocationId, cancellationToken);

        var timezoneId = request.Timezone ?? location.Timezone ?? user.HomeLocation?.Timezone;

        var startDateUtc = DateTimeOffset.Parse(request.StartDateTime).ToUniversalTime();
        var endDateUtc = DateTimeOffset.Parse(request.EndDateTime).ToUniversalTime();

        var awayLocation = new UserAwayLocation
        {
            UserId = userId,
            LocationId = location.Id,
            StartAtUtc = startDateUtc,
            EndAtUtc = endDateUtc,
            Timezone = timezoneId,
            Comment = request.Comment?.Trim(),
        };

        db.UserAwayLocations.Add(awayLocation);
        await db.SaveChangesAsync(cancellationToken);

        awayLocation.Location = location;
        return MapResponse(awayLocation);
    }

    public async Task<AwayLocationResponseDto> UpdateAsync(
        Guid userId,
        int awayLocationId,
        AwayLocationRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        await GetUserOrThrowAsync(userId, cancellationToken);

        var awayLocation = await db
            .UserAwayLocations.Include(x => x.Location)
            .SingleOrDefaultAsync(x => x.Id == awayLocationId && x.UserId == userId, cancellationToken);

        if (awayLocation is null)
        {
            throw new KeyNotFoundException($"Away location {awayLocationId} not found for user {userId}.");
        }

        var location = await GetLocationOrThrowAsync(request.LocationId, cancellationToken);

        var timezoneId = request.Timezone ?? location.Timezone ?? awayLocation.Timezone;

        awayLocation.LocationId = location.Id;
        awayLocation.StartAtUtc = DateTimeOffset.Parse(request.StartDateTime).ToUniversalTime();
        awayLocation.EndAtUtc = DateTimeOffset.Parse(request.EndDateTime).ToUniversalTime();
        awayLocation.Timezone = timezoneId;
        awayLocation.Comment = request.Comment?.Trim();
        awayLocation.Location = location;

        await db.SaveChangesAsync(cancellationToken);
        return MapResponse(awayLocation);
    }

    public async Task<AwayLocationResponseDto> ExpireAsync(
        Guid userId,
        ExpireAwayLocationRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        await GetUserOrThrowAsync(userId, cancellationToken);

        var awayLocation = await db
            .UserAwayLocations.Include(x => x.Location)
            .SingleOrDefaultAsync(x => x.Id == request.AwayLocationId && x.UserId == userId, cancellationToken);

        if (awayLocation is null)
        {
            throw new KeyNotFoundException($"Away location {request.AwayLocationId} not found for user {userId}.");
        }

        awayLocation.ExpiryAtUtc = DateTimeOffset.UtcNow;
        awayLocation.ExpiryReason = request.ExpiryReason.Trim();

        await db.SaveChangesAsync(cancellationToken);
        return MapResponse(awayLocation);
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

    private async Task<Location> GetLocationOrThrowAsync(int locationId, CancellationToken cancellationToken = default)
    {
        var location = await db.Locations.SingleOrDefaultAsync(l => l.Id == locationId, cancellationToken);

        if (location is null)
        {
            throw new KeyNotFoundException($"Location {locationId} not found.");
        }

        return location;
    }

    private static AwayLocationResponseDto MapResponse(UserAwayLocation awayLocation) =>
        new()
        {
            Id = awayLocation.Id,
            UserId = awayLocation.UserId,
            LocationId = awayLocation.LocationId,
            LocationName = awayLocation.Location?.Name ?? string.Empty,
            LocationTimezone = awayLocation.Location?.Timezone ?? string.Empty,
            StartAtUtc = awayLocation.StartAtUtc,
            EndAtUtc = awayLocation.EndAtUtc,
            ExpiryAtUtc = awayLocation.ExpiryAtUtc,
            ExpiryReason = awayLocation.ExpiryReason,
            Comment = awayLocation.Comment,
            Timezone = awayLocation.Timezone,
        };
}
