import CalendarSchedulingView from './CalendarSchedulingView.vue';
import type { CalendarViewDefinition } from '@/modules/calendar/registry/calendarRegistryTypes';
import { buildCalendarSchedulingViewModel } from './calendarSchedulingMappers.ts';
import { canViewShifts } from './calendarSchedulingPermissions';

export const calendarShiftViewContribution: CalendarViewDefinition = {
  id: 'calendar.matrix-schedule',
  label: 'Schedule View',
  order: 20,
  component: CalendarSchedulingView,
  loadingVariant: 'matrix',
  isAvailable: canViewShifts,
  buildModel: (data, queryContext, _runtimeContext, period) =>
    buildCalendarSchedulingViewModel(data, queryContext, period),
};
