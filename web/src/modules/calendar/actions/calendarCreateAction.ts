import { CalendarModuleId } from '../calendarIdentifiers';
import type { CalendarCreateAction } from '../registry/calendarActionRegistryTypes';

export const calendarCreateAction: CalendarCreateAction = {
  id: 'calendar.create',
  label: 'Create event',
  moduleId: CalendarModuleId.Calendar,
  disabled: true,
  isAvailable: (context) => context.activeViewId === 'calendar-default',
};
