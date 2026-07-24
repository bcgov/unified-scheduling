import CalendarMatrixAssignmentTestView from './CalendarMatrixTestAssignmentView.vue';
import type { CalendarViewDefinition } from '@/modules/calendar/registry/calendarRegistryTypes';
import { buildCalendarMatrixAssignmentTestViewModel } from './calendarMatrixTestMappers';

export const calendarMatrixAssignmentTestViewContribution: CalendarViewDefinition = {
  id: 'calendar.matrix-assignment-test',
  label: 'Matrix Assignment View Test',
  order: 20,
  component: CalendarMatrixAssignmentTestView,
  buildModel: (_data, queryContext, _runtimeContext, period) =>
    buildCalendarMatrixAssignmentTestViewModel(queryContext, period),
};
