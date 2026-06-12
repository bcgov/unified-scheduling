import { postApiCalendarEvents } from '@/api-access/calendar';
import type { CalendarEventBase } from '../calendarTypes';
import type { CalendarModuleContribution } from '../registry/calendarRegistryTypes';
import { localDateOnlyToUtcInstant } from '../calendarDateUtils';
import { mapApiCalendarEventToCalendarEventBase } from './calendarEventMappers';

export const calendarEventsContribution: CalendarModuleContribution = {
  moduleId: 'calendar',
  contributionId: 'calendar.events',
  isAvailable(runtimeContext) {
    return runtimeContext.featureFlags.calendarModule ?? true;
  },
  async load(context, options) {
    const events = await postApiCalendarEvents(
      {
        startDate: localDateOnlyToUtcInstant(context.startDate),
        endDate: localDateOnlyToUtcInstant(context.endDate),
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
