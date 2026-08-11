import type { CalendarConflict, CalendarDataResponse, CalendarEventBase } from './calendarTypes';

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

export const selectCalendarEvents = (response: CalendarDataResponse): CalendarEventBase[] => {
  return Object.values(response.contributions)
    .flatMap((contribution) => contribution.events)
    .sort((left, right) => {
      const startComparison = left.start.localeCompare(right.start);
      if (startComparison !== 0) {
        return startComparison;
      }

      return left.title.localeCompare(right.title);
    });
};
