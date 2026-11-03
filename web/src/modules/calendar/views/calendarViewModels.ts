import type {
  CalendarDataResponse,
  CalendarFullCalendarViewModel,
  CalendarQueryContext,
  CalendarRuntimeContext,
} from '../calendarTypes';
import type { CalendarPeriod } from '../calendarStore';
import { selectCalendarEvents } from '../calendarSelectors';
import { isValidIanaTimeZone } from '@/utils/date';

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
): CalendarFullCalendarViewModel | null => {
  const display = resolveCalendarDisplay(period);
  const configuredTimeZone = context.filters.timeZoneId;
  if (typeof configuredTimeZone === 'string' && !isValidIanaTimeZone(configuredTimeZone)) {
    return null;
  }

  return {
    view: display.view,
    timeZoneId: typeof configuredTimeZone === 'string' ? configuredTimeZone : 'local',
    initialDate: context.startDate,
    events: selectCalendarEvents(response),
    weekends: display.weekends,
  };
};
