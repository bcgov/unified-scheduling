import { defineAsyncComponent } from 'vue';
import type { CalendarViewDefinition } from '@/modules/calendar/registry/calendarRegistryTypes';
import { buildCalendarSchedulingViewModel } from './calendarSchedulingMappers.ts';
import { canViewShifts } from './calendarSchedulingPermissions';

export const calendarShiftViewContribution: CalendarViewDefinition = {
  id: 'calendar.matrix-schedule',
  label: 'Schedule View',
  order: 20,
  // Lazy-load: this contribution is registered eagerly at app boot when the
  // Scheduling feature flag is on, so avoid pulling the heavy matrix view
  // component into the eager bundle.
  component: defineAsyncComponent(() => import('./CalendarSchedulingView.vue')),
  loadingVariant: 'matrix',
  isAvailable: canViewShifts,
  buildModel: (data, queryContext, _runtimeContext, period) =>
    buildCalendarSchedulingViewModel(data, queryContext, period),
};
