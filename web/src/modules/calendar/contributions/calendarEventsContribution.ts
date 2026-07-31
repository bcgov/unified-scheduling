import { postApiCalendarEvents } from '@/api-access/calendar';
import { addDays } from '@/utils/date';
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
    const events = await postApiCalendarEvents(
      {
        startDate: context.startDate,
        // Calendar view ranges end exclusively; the API accepts an inclusive date-only range.
        endDate: addDays(context.endDate, -1),
        locationId: context.locationId,
        filters: context.filters,
      },
      { fetchOptions: { signal: options?.signal } },
    );

    return {
      moduleId: 'calendar',
      contributionId: 'calendar.events',
      events: events.map<CalendarEventBase>(mapApiCalendarEventToCalendarEventBase),
    };
  },
};
