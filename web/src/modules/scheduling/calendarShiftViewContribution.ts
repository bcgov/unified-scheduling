import CalendarShiftView from './CalendarShiftView.vue';
import type { CalendarViewDefinition } from '@/modules/calendar/registry/calendarRegistryTypes';
import { buildCalendarSchedulingViewModel } from './calendarSchedulingMappers.ts';

export const calendarShiftViewContribution: CalendarViewDefinition = {
  id: 'calendar.matrix-schedule',
  label: 'Schedule View',
  order: 20,
  component: CalendarShiftView,
  buildModel: (data, queryContext, _runtimeContext, period) =>
    buildCalendarSchedulingViewModel(data, queryContext, period),
};
