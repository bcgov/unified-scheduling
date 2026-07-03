import type { CalendarCreateAction } from '../registry/calendarActionRegistryTypes';

export const calendarCreateAction: CalendarCreateAction = {
  id: 'calendar.create',
  label: 'Create event',
  moduleId: 'calendar',
  disabled: true,
  isAvailable: (context) => context.activeViewId === 'calendar-default',
};
