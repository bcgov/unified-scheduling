import { postApiCalendarData } from '@/api-access/calendar';
import type { CalendarEventBase } from '../calendarTypes';
import type { CalendarModuleContribution } from '../registry/calendarRegistryTypes';
import { mapApiCalendarEventToCalendarEventBase } from './calendarEventMappers';

export const calendarEventsContribution: CalendarModuleContribution = {
  moduleId: 'calendar',
  contributionId: 'calendar.events',
  isAvailable(runtimeContext) {
    return runtimeContext.featureFlags.calendarModule ?? true;
  },
  async load(context, options) {
    const data = await postApiCalendarData(
      {
        startDate: context.startDate,
        endDate: context.endDate,
        locationId: context.locationId,
        filters: context.filters,
      },
      { fetchOptions: { signal: options?.signal } },
    );

    return {
      moduleId: data.moduleId,
      contributionId: data.contributionId,
      events: data.events.map<CalendarEventBase>(mapApiCalendarEventToCalendarEventBase),
    };
  },
};
