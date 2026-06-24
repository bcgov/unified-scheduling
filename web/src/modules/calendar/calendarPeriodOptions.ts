import type { SelectOption, SelectValue } from '@/types/select';
import type { CalendarPeriod } from './calendarStore';

export const DEFAULT_CALENDAR_PERIODS = ['week', 'day', 'work-week'] as const satisfies readonly CalendarPeriod[];

const CALENDAR_PERIOD_LABELS: Record<CalendarPeriod, string> = {
  day: 'Day',
  week: 'Week',
  'work-week': 'Work week',
  month: 'Month',
};

export function buildCalendarPeriodSelectOptions(periods: readonly CalendarPeriod[]): SelectOption[] {
  return periods.map((period) => ({
    code: period,
    description: CALENDAR_PERIOD_LABELS[period],
  }));
}

export function isCalendarPeriod(value: SelectValue | undefined): value is CalendarPeriod {
  return value === 'day' || value === 'week' || value === 'work-week' || value === 'month';
}
