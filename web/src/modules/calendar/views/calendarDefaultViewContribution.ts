import CalendarFullCalendarView from '../components/CalendarFullCalendarView.vue';
import { buildCalendarDefaultViewModel } from './calendarViewModels';

export const calendarDefaultViewContribution = {
  id: 'calendar-default',
  label: 'Calendar',
  order: 10,
  component: CalendarFullCalendarView,
  buildModel: buildCalendarDefaultViewModel,
};
