using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class AwayLocationService(UnifiedDbContext db) : IAwayLocationService
{
    private const string SourceModule = "user-management";

    public async Task<IReadOnlyCollection<AwayLocationResponseDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await GetUserOrThrowAsync(userId, cancellationToken);

        var awayLocations = await db
            .UserAwayLocations.AsNoTracking()
            .Include(x => x.Event)
                .ThenInclude(e => e.Location)
            .Where(x =>
                x.UserId == userId && (x.Event.CancelledAt == null || x.Event.CancelledAt > DateTimeOffset.UtcNow)
            )
            .OrderByDescending(x => x.Event.StartAtUtc)
            .ThenBy(x => x.Event.LocationId)
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

        var calendarEvent = new Event
        {
            Title = $"Away - {location.Name}",
            Notes = request.Comment?.Trim(),
            StartAtUtc = startDateUtc,
            EndAtUtc = endDateUtc,
            TimeZoneId = timezoneId,
            AllDay = request.AllDay,
            EventTypeCode = CalendarEventTypeCodes.AwayLocation,
            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            SourceModule = SourceModule,
            LocationId = location.Id,
        };

        db.Events.Add(calendarEvent);
        await db.SaveChangesAsync(cancellationToken);

        var awayLocation = new UserAwayLocation { UserId = userId, EventId = calendarEvent.Id };

        db.UserAwayLocations.Add(awayLocation);
        await db.SaveChangesAsync(cancellationToken);

        calendarEvent.Location = location;
        awayLocation.Event = calendarEvent;
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
            .UserAwayLocations.Include(x => x.Event)
                .ThenInclude(e => e.Location)
            .SingleOrDefaultAsync(x => x.Id == awayLocationId && x.UserId == userId, cancellationToken);

        if (awayLocation is null)
        {
            throw new KeyNotFoundException($"Away location {awayLocationId} not found for user {userId}.");
        }

        if (awayLocation.Event.CancelledAt is not null)
        {
            throw new InvalidOperationException(
                $"Away location {awayLocationId} is already expired and cannot be edited."
            );
        }

        var location = await GetLocationOrThrowAsync(request.LocationId, cancellationToken);

        var timezoneId = request.Timezone ?? location.Timezone ?? awayLocation.Event.TimeZoneId;

        awayLocation.Event.Title = $"Away - {location.Name}";
        awayLocation.Event.LocationId = location.Id;
        awayLocation.Event.StartAtUtc = DateTimeOffset.Parse(request.StartDateTime).ToUniversalTime();
        awayLocation.Event.EndAtUtc = DateTimeOffset.Parse(request.EndDateTime).ToUniversalTime();
        awayLocation.Event.TimeZoneId = timezoneId;
        awayLocation.Event.AllDay = request.AllDay;
        awayLocation.Event.Notes = request.Comment?.Trim();
        awayLocation.Event.Location = location;

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
            .UserAwayLocations.Include(x => x.Event)
                .ThenInclude(e => e.Location)
            .SingleOrDefaultAsync(x => x.Id == request.AwayLocationId && x.UserId == userId, cancellationToken);

        if (awayLocation is null)
        {
            throw new KeyNotFoundException($"Away location {request.AwayLocationId} not found for user {userId}.");
        }

        if (awayLocation.Event.CancelledAt is not null)
        {
            throw new InvalidOperationException($"Away location {request.AwayLocationId} is already expired.");
        }

        awayLocation.Event.CancelledAt = DateTimeOffset.UtcNow;
        awayLocation.Event.CancellationReason = request.ExpiryReason.Trim();
        awayLocation.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;

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
            EventId = awayLocation.EventId,
            UserId = awayLocation.UserId,
            LocationId = awayLocation.Event.LocationId ?? 0,
            LocationName = awayLocation.Event.Location?.Name ?? string.Empty,
            LocationTimezone = awayLocation.Event.Location?.Timezone ?? string.Empty,
            StartAtUtc = awayLocation.Event.StartAtUtc,
            EndAtUtc = awayLocation.Event.EndAtUtc,
            AllDay = awayLocation.Event.AllDay,
            ExpiryAtUtc = awayLocation.Event.CancelledAt,
            ExpiryReason = awayLocation.Event.CancellationReason,
            Comment = awayLocation.Event.Notes,
            Timezone = awayLocation.Event.TimeZoneId,
        };
}
