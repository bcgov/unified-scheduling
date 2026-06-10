import type { CalendarViewDetailAction } from '../registry/calendarRegistryTypes';
import { useCalendarStore } from '../calendarStore';

export const calendarEventDetailModalAction: CalendarViewDetailAction = {
  id: 'calendar.event-detail.modal',
  moduleId: 'calendar',
  run(context) {
    const calendarStore = useCalendarStore();
    calendarStore.setSelectedEvent(context.event.id);
  },
};
