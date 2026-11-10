import type {
  CalendarConflict,
  CalendarConflictEventIdentity,
  CalendarDataResponse,
  CalendarEventBase,
  CalendarRestrictedConflictWarning,
} from './calendarTypes';

export const selectCalendarRestrictedWarnings = (
  response: CalendarDataResponse,
): CalendarRestrictedConflictWarning[] => {
  const data = response.contributions['calendar.events']?.data;
  if (!data || typeof data !== 'object' || !('restrictedWarnings' in data)) return [];
  return Array.isArray(data.restrictedWarnings) ? data.restrictedWarnings : [];
};

export const getCalendarRestrictedWarningsForEvent = (
  eventId: number | CalendarConflictEventIdentity | null | undefined,
  warnings: readonly CalendarRestrictedConflictWarning[],
  resourceId?: string,
) => {
  if (eventId == null) return [];
  const identity = typeof eventId === 'number' ? { sourceModule: 'scheduling', eventId: String(eventId) } : eventId;
  return warnings.filter(
    (warning) =>
      warning.sourceModule === identity.sourceModule &&
      warning.eventId === identity.eventId &&
      (!resourceId || warning.resourceId === resourceId),
  );
};

export const selectContribution = (response: CalendarDataResponse, contributionId: string) => {
  return response.contributions[contributionId];
};

export const selectCalendarConflicts = (response: CalendarDataResponse): CalendarConflict[] => {
  const data = response.contributions['calendar.events']?.data;
  if (typeof data !== 'object' || data === null || !('conflicts' in data)) {
    return [];
  }

  const conflicts = (data as { conflicts?: unknown }).conflicts;
  return Array.isArray(conflicts) ? (conflicts as CalendarConflict[]) : [];
};

export const resolveCalendarEventId = (event: unknown) => {
  if (!event || typeof event !== 'object' || !('metadata' in event)) return null;
  const metadata = event.metadata;
  if (!metadata || typeof metadata !== 'object' || !('eventId' in metadata)) return null;
  const parsed = Number(metadata.eventId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
};

export const getCalendarConflictsForEvent = (
  eventId: number | CalendarConflictEventIdentity | null | undefined,
  conflicts: readonly CalendarConflict[],
) => {
  if (eventId == null) {
    return [];
  }

  const identity = typeof eventId === 'number' ? { sourceModule: 'scheduling', eventId: String(eventId) } : eventId;
  return conflicts.filter(
    (conflict) =>
      (conflict.entry.sourceModule === identity.sourceModule &&
        conflict.entry.eventId === identity.eventId &&
        conflict.reportOnEntry) ||
      (conflict.overlaps.sourceModule === identity.sourceModule &&
        conflict.overlaps.eventId === identity.eventId &&
        conflict.reportOnOverlaps),
  );
};

export const getCalendarConflictsForEventAndResource = (
  eventId: number | CalendarConflictEventIdentity | null | undefined,
  resourceId: string,
  conflicts: readonly CalendarConflict[],
) => getCalendarConflictsForEvent(eventId, conflicts).filter((conflict) => conflict.resourceId === resourceId);

function getLeaveEntryId(event: CalendarEventBase): string | undefined {
  if (!('metadata' in event) || typeof event.metadata !== 'object' || event.metadata === null) return;
  if (!('leaveEntryId' in event.metadata)) return;
  const id = event.metadata.leaveEntryId;
  return typeof id === 'string' || typeof id === 'number' ? String(id) : undefined;
}
function getLeaveUserIds(event: CalendarEventBase): string[] {
  if (event.resourceIds?.length) return event.resourceIds;
  if (!('metadata' in event) || typeof event.metadata !== 'object' || event.metadata === null) return [];
  if (!('userIds' in event.metadata) || !Array.isArray(event.metadata.userIds)) return [];
  return event.metadata.userIds.filter((id): id is string => typeof id === 'string');
}
export const selectCalendarEvents = (response: CalendarDataResponse): CalendarEventBase[] => {
  const contributions = Object.values(response.contributions);
  const existingLeaveUsers = new Map<string, Set<string>>();
  for (const contribution of contributions) {
    if (contribution.moduleId === 'loanout') continue;
    for (const event of contribution.events) {
      const leaveEntryId = getLeaveEntryId(event);
      if (leaveEntryId === undefined) continue;
      const users = existingLeaveUsers.get(leaveEntryId) ?? new Set<string>();
      getLeaveUserIds(event).forEach((id) => users.add(id));
      existingLeaveUsers.set(leaveEntryId, users);
    }
  }
  return contributions
    .flatMap((contribution) =>
      contribution.events.flatMap((event) => {
        if (contribution.moduleId !== 'loanout') return [event];
        const leaveEntryId = getLeaveEntryId(event);
        const existing = leaveEntryId === undefined ? undefined : existingLeaveUsers.get(leaveEntryId);
        if (!existing?.size) return [event];
        const resourceIds = event.resourceIds?.filter((id) => !existing.has(id)) ?? [];
        if (!resourceIds.length) return [];
        // Keep displayed user IDs consistent with the resources that remain visible.
        const metadata =
          'metadata' in event && typeof event.metadata === 'object' && event.metadata !== null
            ? { ...event.metadata, userIds: resourceIds }
            : undefined;
        return [{ ...event, resourceIds, ...(metadata ? { metadata } : {}) }];
      }),
    )
    .sort((left, right) => {
      const startComparison = left.start.localeCompare(right.start);
      if (startComparison !== 0) return startComparison;

      return left.title.localeCompare(right.title);
    });
};
