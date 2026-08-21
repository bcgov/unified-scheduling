using Mapster;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

internal static class ShiftAssignmentResponseMapper
{
    private static readonly TypeAdapterConfig EntryResponseConfig = BuildEntryResponseConfig();

    public static ShiftAssignmentEntryResponse ToEntryResponse(ShiftAssignmentEntry link, int capacity) =>
        link.Adapt<ShiftAssignmentEntryResponse>(EntryResponseConfig) with
        {
            Capacity = capacity,
        };

    public static ShiftAssignmentSeriesLinkResponse ToSeriesLinkResponse(
        ShiftAssignmentSeriesLink seriesLink,
        IReadOnlyCollection<ShiftAssignmentEntry> entryLinks,
        IReadOnlyCollection<AssignmentEntry> assignmentEntries
    )
    {
        var assignmentCapacityById = assignmentEntries.ToDictionary(entry => entry.Id, entry => entry.Capacity);
        var entryLinkResponses = entryLinks
            .Select(link => ToEntryResponse(link, assignmentCapacityById.GetValueOrDefault(link.AssignmentEntryId)))
            .ToList();

        return new ShiftAssignmentSeriesLinkResponse
        {
            Id = seriesLink.Id,
            ShiftSeriesId = seriesLink.ShiftSeriesId,
            AssignmentSeriesId = seriesLink.AssignmentSeriesId,
            AssignedUserIds = seriesLink.Users.Select(user => user.UserId).Distinct().ToList(),
            ShiftAssignmentEntryIds = entryLinkResponses.Select(link => link.Id).ToList(),
            EntryLinks = entryLinkResponses,
            ExceptionCount = entryLinkResponses.Count(link => link.IsException),
        };
    }

    private static TypeAdapterConfig BuildEntryResponseConfig()
    {
        var config = new TypeAdapterConfig();
        config
            .NewConfig<ShiftAssignmentEntry, ShiftAssignmentEntryResponse>()
            .Ignore(response => response.Capacity)
            .Map(
                response => response.UserIds,
                link => link.Users.Select(user => user.UserId).Distinct().ToList()
            )
            .Map(
                response => response.AssignedUserCount,
                link => link.Users.Select(user => user.UserId).Distinct().Count()
            );

        return config;
    }
}
