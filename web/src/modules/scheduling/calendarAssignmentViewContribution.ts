import CalendarAssignmentView from './CalendarAssignmentView.vue';
import type { CalendarViewDefinition } from '@/modules/calendar/registry/calendarRegistryTypes';
import { buildCalendarAssignmentViewModel } from './calendarSchedulingMappers.ts';
import { canViewAssignments } from './calendarSchedulingPermissions';

export const calendarAssignmentViewContribution: CalendarViewDefinition = {
  id: 'calendar.matrix-assignment',
  label: 'Assignment View',
  order: 20,
  component: CalendarAssignmentView,
  loadingVariant: 'matrix',
  isAvailable: canViewAssignments,
  buildModel: (data, queryContext, _runtimeContext, period) =>
    buildCalendarAssignmentViewModel(data, queryContext, period),
};
