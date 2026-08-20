import { postApiCalendarEvents } from '@/api-access/generated/calendar/calendar';
import type { CalendarDataRequestFilters } from '@/api-access/generated/models';
import { addDays, toApiDateString } from '@/utils/date';
import type { CalendarEventBase } from '../calendarTypes';
import type { CalendarModuleContribution } from '../registry/calendarRegistryTypes';
import { mapApiCalendarEventToCalendarEventBase } from './calendarEventMappers';

export const calendarEventsContribution: CalendarModuleContribution = {
  moduleId: 'calendar',
  contributionId: 'calendar.events',
  isAvailable(runtimeContext) {
    return runtimeContext.featureFlags.Calendar?.enabled ?? true;
  },
  async load(context, options) {
    const { data, error, execute } = postApiCalendarEvents(
      {
        startDate: toApiDateString(context.startDate),
        // Calendar view ranges end exclusively; the API accepts an inclusive date-only range.
        endDate: toApiDateString(addDays(context.endDate, -1)),
        locationId: context.locationId,
        filters: context.filters as CalendarDataRequestFilters,
      },
      {
        fetchOptions: { signal: options?.signal },
        options: { immediate: false },
      },
    );

    await execute();

    if (error.value) {
      throw error.value;
    }

    return {
      moduleId: 'calendar',
      contributionId: 'calendar.events',
      events: (data.value?.events ?? []).map<CalendarEventBase>(mapApiCalendarEventToCalendarEventBase),
    };
  },
};
