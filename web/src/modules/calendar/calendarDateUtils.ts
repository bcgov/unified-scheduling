type CalendarPeriodValue = 'day' | 'week' | 'work-week' | 'month';

export type CalendarDateRange = {
  startDate: string;
  endDate: string;
};

type DateParts = {
  year: number;
  month: number;
  day: number;
};

function padDatePart(value: number) {
  return String(value).padStart(2, '0');
}

function parseDateParts(value: string): DateParts {
  const [year, month, day] = value.split('-').map(Number);
  return { year, month, day };
}

export function parseLocalDateOnly(value: string) {
  const { year, month, day } = parseDateParts(value);
  return new Date(year, month - 1, day);
}

export function formatLocalDateOnly(value: Date) {
  return `${value.getFullYear()}-${padDatePart(value.getMonth() + 1)}-${padDatePart(value.getDate())}`;
}

export function getTodayDateOnly() {
  return formatLocalDateOnly(new Date());
}

export function addDays(value: string, days: number) {
  const date = parseLocalDateOnly(value);
  date.setDate(date.getDate() + days);
  return formatLocalDateOnly(date);
}

export function addMonths(value: string, months: number) {
  const date = parseLocalDateOnly(value);
  date.setMonth(date.getMonth() + months);
  return formatLocalDateOnly(date);
}

export function startOfWeek(value: string) {
  const date = parseLocalDateOnly(value);
  const dayOfWeek = date.getDay();
  const offset = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
  date.setDate(date.getDate() + offset);
  return formatLocalDateOnly(date);
}

export function startOfMonth(value: string) {
  const date = parseLocalDateOnly(value);
  date.setDate(1);
  return formatLocalDateOnly(date);
}

export function buildDateRangeForPeriod(anchorDate: string, currentPeriod: CalendarPeriodValue): CalendarDateRange {
  switch (currentPeriod) {
    case 'day':
      return {
        startDate: anchorDate,
        endDate: addDays(anchorDate, 1),
      };
    case 'work-week': {
      const startDate = startOfWeek(anchorDate);
      return {
        startDate,
        endDate: addDays(startDate, 5),
      };
    }
    case 'month': {
      const startDate = startOfMonth(anchorDate);
      return {
        startDate,
        endDate: addMonths(startDate, 1),
      };
    }
    case 'week':
    default: {
      const startDate = startOfWeek(anchorDate);
      return {
        startDate,
        endDate: addDays(startDate, 7),
      };
    }
  }
}

export function shiftDateRange(currentStartDate: string, currentPeriod: CalendarPeriodValue, direction: -1 | 1) {
  switch (currentPeriod) {
    case 'day':
      return buildDateRangeForPeriod(addDays(currentStartDate, direction), currentPeriod);
    case 'work-week':
    case 'week':
      return buildDateRangeForPeriod(addDays(currentStartDate, direction * 7), currentPeriod);
    case 'month':
      return buildDateRangeForPeriod(addMonths(currentStartDate, direction), currentPeriod);
  }
}

export function getInitialCalendarDateRange(): CalendarDateRange {
  return buildDateRangeForPeriod(getTodayDateOnly(), 'week');
}

export function formatRangeLabel(startDate: string, endDate: string, currentPeriod: CalendarPeriodValue) {
  const formatter = new Intl.DateTimeFormat('en-CA', {
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  });
  const monthFormatter = new Intl.DateTimeFormat('en-CA', {
    month: 'long',
    year: 'numeric',
  });

  if (currentPeriod === 'month') {
    return monthFormatter.format(parseLocalDateOnly(startDate));
  }

  if (currentPeriod === 'day') {
    return formatter.format(parseLocalDateOnly(startDate));
  }

  return `${formatter.format(parseLocalDateOnly(startDate))} - ${formatter.format(parseLocalDateOnly(addDays(endDate, -1)))}`;
}

export function localDateOnlyToUtcInstant(value: string) {
  const { year, month, day } = parseDateParts(value);
  return new Date(Date.UTC(year, month - 1, day)).toISOString();
}

export function toCalendarDateOnly(value?: string | null) {
  if (!value) {
    return undefined;
  }

  const [dateOnly] = value.split('T');
  return dateOnly;
}

export function formatCalendarDateOnly(value: string, locale = 'en-CA') {
  return new Intl.DateTimeFormat(locale, {
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  }).format(parseLocalDateOnly(value));
}
