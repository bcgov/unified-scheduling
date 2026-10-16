import { CalendarActionId, CalendarModuleId } from '../calendarIdentifiers';
import type {
  CalendarMatrixCellHeaderAction,
  CalendarViewDetailAction,
} from '../registry/calendarActionRegistryTypes';
import { useCalendarStore } from '../calendarStore';

export const calendarEventDetailModalAction: CalendarViewDetailAction = {
  id: CalendarActionId.EventDetail,
  moduleId: CalendarModuleId.Calendar,
  run(context) {
    const calendarStore = useCalendarStore();
    calendarStore.setSelectedEvent(context.event.id);
  },
};

export const calendarEventHeaderDetailAction: CalendarMatrixCellHeaderAction = {
  id: CalendarActionId.EventDetail,
  moduleId: CalendarModuleId.Calendar,
  label: 'View event details',
  isAvailable: (context) =>
    context.actionId === CalendarActionId.EventDetail &&
    (context.header.payload as { sourceModule?: unknown } | undefined)?.sourceModule === CalendarModuleId.Calendar,
  execute: (context) => {
    const event = context.header.payload as { id?: unknown; sourceModule?: unknown } | undefined;
    if (event?.sourceModule === CalendarModuleId.Calendar && typeof event.id === 'string') {
      useCalendarStore().setSelectedEvent(event.id);
    }
  },
};
