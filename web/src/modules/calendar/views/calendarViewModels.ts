import type {
  CalendarDataResponse,
  CalendarFullCalendarViewModel,
  CalendarQueryContext,
  CalendarRuntimeContext,
} from '../calendarTypes';
import type { CalendarPeriod } from '../calendarStore';
import { selectCalendarEvents } from '../calendarSelectors';

interface CalendarDisplayConfig {
  view: 'timeGridDay' | 'timeGridWeek' | 'dayGridMonth';
  weekends: boolean;
}

const calendarDisplayByPeriod: Record<CalendarPeriod, CalendarDisplayConfig> = {
  day: {
    view: 'timeGridDay',
    weekends: true,
  },
  week: {
    view: 'timeGridWeek',
    weekends: true,
  },
  'work-week': {
    view: 'timeGridWeek',
    weekends: false,
  },
  month: {
    view: 'dayGridMonth',
    weekends: true,
  },
};

function resolveCalendarDisplay(period: CalendarPeriod): CalendarDisplayConfig {
  return calendarDisplayByPeriod[period];
}

export const buildCalendarDefaultViewModel = (
  response: CalendarDataResponse,
  context: CalendarQueryContext,
  _runtimeContext: CalendarRuntimeContext,
  period: CalendarPeriod,
): CalendarFullCalendarViewModel => {
  const display = resolveCalendarDisplay(period);

  return {
    view: display.view,
    initialDate: context.startDate,
    events: selectCalendarEvents(response),
    weekends: display.weekends,
  };
};
