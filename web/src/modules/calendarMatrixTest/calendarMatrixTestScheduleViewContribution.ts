import CalendarMatrixScheduleTestView from './CalendarMatrixTestScheduleView.vue';
import type { CalendarViewDefinition } from '@/modules/calendar/registry/calendarRegistryTypes';
import { buildCalendarMatrixScheduleTestViewModel } from './calendarMatrixTestMappers';

export const calendarMatrixScheduleTestViewContribution: CalendarViewDefinition = {
  id: 'calendar.matrix-schedule-test',
  label: 'Matrix Schedule View Test',
  order: 20,
  component: CalendarMatrixScheduleTestView,
  buildModel: (_data, queryContext, _runtimeContext, period) =>
    buildCalendarMatrixScheduleTestViewModel(queryContext, period),
};
