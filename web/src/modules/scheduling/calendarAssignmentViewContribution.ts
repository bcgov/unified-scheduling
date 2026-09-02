import CalendarAssignmentView from './CalendarAssignmentView.vue';
import type { CalendarViewDefinition } from '@/modules/calendar/registry/calendarRegistryTypes';
import { buildCalendarAssignmentViewModel } from './calendarSchedulingMappers.ts';

export const calendarAssignmentViewContribution: CalendarViewDefinition = {
  id: 'calendar.matrix-assignment',
  label: 'Assignment View',
  order: 20,
  component: CalendarAssignmentView,
  buildModel: (data, queryContext, _runtimeContext, period) =>
    buildCalendarAssignmentViewModel(data, queryContext, period),
};
