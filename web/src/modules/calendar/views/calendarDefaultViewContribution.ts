import { defineAsyncComponent } from 'vue';
import { buildCalendarDefaultViewModel } from './calendarViewModels';

export const calendarDefaultViewContribution = {
  id: 'calendar-default',
  label: 'Calendar',
  order: 10,
  // Lazy-load the FullCalendar-backed view component so its heavy dependency
  // (FullCalendar core + plugins) is only fetched when the calendar view is
  // actually rendered, not at module-registration time (which runs eagerly
  // for every page load via router/index.ts -> CalendarModule.registerModule).
  component: defineAsyncComponent(() => import('../components/CalendarFullCalendarView.vue')),
  buildModel: buildCalendarDefaultViewModel,
};
