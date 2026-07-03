import { describe, expect, it } from 'vitest';

import {
  addDays,
  buildDateRangeForPeriod,
  formatCalendarDateOnly,
  formatLocalDateOnly,
  formatRangeLabel,
  localDateOnlyToUtcInstant,
  parseLocalDateOnly,
  shiftDateRange,
  startOfMonth,
  startOfWeek,
  toCalendarDateOnly,
} from '@/utils/date';
import {
  buildCalendarSchedulingViewModel,
  getCalendarEventDateKey,
} from '@/modules/scheduling/calendarSchedulingMappers';
import type { CalendarSchedulingUserResource } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import { selectCalendarEvents, selectContribution } from '@/modules/calendar/calendarSelectors';
import { mapApiCalendarEventToCalendarEventBase } from '@/modules/calendar/contributions/calendarEventMappers';
import { buildCalendarPeriodSelectOptions, DEFAULT_CALENDAR_PERIODS } from '@/modules/calendar/calendarPeriodOptions';
import { buildCalendarDefaultViewModel } from '@/modules/calendar/views/calendarViewModels';
import type {
  CalendarDataResponse,
  CalendarQueryContext,
  CalendarRuntimeContext,
} from '@/modules/calendar/calendarTypes';
import { mdiCalendarSync } from '@mdi/js';

describe('shared calendar date helpers', () => {
  it('builds ranges for every period and shifts them correctly', () => {
    expect(buildDateRangeForPeriod('2025-01-15', 'day')).toEqual({
      startDate: '2025-01-15',
      endDate: '2025-01-16',
    });

    expect(buildDateRangeForPeriod('2025-01-15', 'work-week')).toEqual({
      startDate: '2025-01-13',
      endDate: '2025-01-18',
    });

    expect(buildDateRangeForPeriod('2025-01-15', 'week')).toEqual({
      startDate: '2025-01-13',
      endDate: '2025-01-20',
    });

    expect(buildDateRangeForPeriod('2025-01-15', 'month')).toEqual({
      startDate: '2025-01-01',
      endDate: '2025-02-01',
    });

    expect(shiftDateRange('2025-01-15', 'day', -1)).toEqual({
      startDate: '2025-01-14',
      endDate: '2025-01-15',
    });

    expect(shiftDateRange('2025-01-13', 'week', 1)).toEqual({
      startDate: '2025-01-20',
      endDate: '2025-01-27',
    });

    expect(shiftDateRange('2025-01-13', 'work-week', 1)).toEqual({
      startDate: '2025-01-20',
      endDate: '2025-01-25',
    });

    expect(shiftDateRange('2025-01-01', 'month', 1)).toEqual({
      startDate: '2025-02-01',
      endDate: '2025-03-01',
    });
  });

  it('formats and parses calendar dates and labels', () => {
    expect(formatLocalDateOnly(parseLocalDateOnly('2025-01-15'))).toBe('2025-01-15');
    expect(startOfWeek('2025-01-19')).toBe('2025-01-13');
    expect(startOfMonth('2025-01-19')).toBe('2025-01-01');

    expect(formatRangeLabel('2025-01-01', '2025-02-01', 'month')).toContain('2025');
    expect(formatRangeLabel('2025-01-15', '2025-01-16', 'day')).toContain('January');
    expect(formatRangeLabel('2025-01-13', '2025-01-20', 'week')).toContain(' - ');

    expect(localDateOnlyToUtcInstant('2025-01-15')).toBe('2025-01-15T00:00:00.000Z');
    expect(toCalendarDateOnly('2025-01-15T12:30:00Z')).toBe('2025-01-15');
    expect(toCalendarDateOnly()).toBeUndefined();
    expect(formatCalendarDateOnly('2025-01-15')).toContain('2025');
    expect(addDays('2025-01-31', 1)).toBe('2025-02-01');
  });

  it('resolves event date keys safely for timezone-aware matrix grouping', () => {
    expect(getCalendarEventDateKey('2025-01-14T07:30:00Z', 'America/Vancouver')).toBe('2025-01-13');
    expect(getCalendarEventDateKey('2025-01-13T23:30:00-08:00', 'America/Vancouver')).toBe('2025-01-13');
    expect(getCalendarEventDateKey('2025-01-13T09:00:00', 'America/Vancouver')).toBe('2025-01-13');
    expect(getCalendarEventDateKey('invalid', 'America/Vancouver')).toBeUndefined();
    expect(getCalendarEventDateKey(undefined, 'America/Vancouver')).toBeUndefined();
  });
});

describe('calendar selectors and view models', () => {
  const response: CalendarDataResponse = {
    contributions: {
      one: {
        moduleId: 'calendar',
        contributionId: 'one',
        events: [
          { id: '2', type: 'calendar.general', sourceModule: 'calendar', title: 'Zulu', start: '2025-01-20' },
          { id: '1', type: 'calendar.general', sourceModule: 'calendar', title: 'Alpha', start: '2025-01-20' },
        ],
      },
      two: {
        moduleId: 'calendar',
        contributionId: 'two',
        events: [
          { id: '3', type: 'calendar.general', sourceModule: 'calendar', title: 'Earlier', start: '2025-01-10' },
        ],
      },
    },
  };

  const queryContext: CalendarQueryContext = {
    startDate: '2025-01-13',
    endDate: '2025-01-20',
    filters: {},
  };

  const runtimeContext: CalendarRuntimeContext = { featureFlags: {} };

  it('selects contributions and sorts flattened events by start then title', () => {
    expect(selectContribution(response, 'one')).toBe(response.contributions.one);
    expect(selectCalendarEvents(response).map((event) => event.id)).toEqual(['3', '1', '2']);
  });

  it.each([
    ['day', 'timeGridDay', true],
    ['week', 'timeGridWeek', true],
    ['work-week', 'timeGridWeek', false],
    ['month', 'dayGridMonth', true],
  ] as const)('builds the default view model for %s', (period, view, weekends) => {
    expect(buildCalendarDefaultViewModel(response, queryContext, runtimeContext, period)).toEqual({
      view,
      initialDate: '2025-01-13',
      events: selectCalendarEvents(response),
      weekends,
    });
  });
});

describe('scheduling calendar view model', () => {
  it('shows the conflict action only when a shift event has isConflict', () => {
    const shiftEvents: CalendarSchedulingEvent[] = [
      {
        id: 'shift-conflict',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Conflict shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        isConflict: true,
        metadata: { userIds: ['user-1'] },
      },
      {
        id: 'shift-normal',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Normal shift',
        start: '2025-01-14T09:00:00',
        end: '2025-01-14T17:00:00',
        resourceIds: ['user-1'],
        metadata: { userIds: ['user-1'], shiftSeriesId: 100 },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events: shiftEvents,
            resources: [
              {
                id: 'user-1',
                type: 'user',
                sourceModule: 'scheduling',
                label: 'Alex Alpha',
                title: 'Alex Alpha',
              },
            ] as CalendarSchedulingUserResource[],
          },
        },
      },
      { startDate: '2025-01-13', endDate: '2025-01-20', filters: {} },
      'week',
    );

    const headers = viewModel.cells.flatMap((cell) => cell.headers ?? []);

    expect(headers.find((header) => header.id === 'shift-conflict')?.action?.ariaLabel).toBe('Show conflict details');
    expect(headers.find((header) => header.id === 'shift-normal')?.action).toBeUndefined();
    expect(headers.find((header) => header.id === 'shift-normal')?.info?.icons).toContainEqual({
      icon: mdiCalendarSync,
      ariaLabel: 'Part of a shift series',
      title: 'Part of a shift series',
    });
  });
});

describe('calendar period options', () => {
  it('excludes month by default and allows views to opt in', () => {
    expect(buildCalendarPeriodSelectOptions(DEFAULT_CALENDAR_PERIODS)).toEqual([
      { code: 'week', description: 'Week' },
      { code: 'day', description: 'Day' },
      { code: 'work-week', description: 'Work week' },
    ]);

    expect(buildCalendarPeriodSelectOptions([...DEFAULT_CALENDAR_PERIODS, 'month'])).toContainEqual({
      code: 'month',
      description: 'Month',
    });
  });
});

describe('calendar event mappers', () => {
  it('maps all-day API events and defaults empty event types', () => {
    expect(
      mapApiCalendarEventToCalendarEventBase({
        id: 10,
        title: 'Holiday',
        startAtUtc: '2025-07-01T00:00:00Z',
        endAtUtc: '2025-07-02T00:00:00Z',
        allDay: true,
        isException: false,
        eventTypeCode: '',
        statusTypeCode: 'active',
        sourceModule: 'calendar',
      }),
    ).toMatchObject({
      id: '10',
      type: 'calendar.general',
      start: '2025-07-01',
      end: '2025-07-02',
      eventTypeCode: 'general',
      statusTypeCode: 'active',
    });
  });

  it('preserves timestamp values for non all-day events', () => {
    expect(
      mapApiCalendarEventToCalendarEventBase({
        id: 11,
        title: 'Meeting',
        startAtUtc: '2025-07-01T09:00:00Z',
        endAtUtc: '2025-07-01T10:00:00Z',
        allDay: false,
        isException: true,
        eventTypeCode: 'deadline',
        statusTypeCode: 'draft',
        sourceModule: 'calendar',
      }),
    ).toMatchObject({
      id: '11',
      type: 'calendar.deadline',
      start: '2025-07-01T09:00:00Z',
      end: '2025-07-01T10:00:00Z',
      isException: true,
      eventTypeCode: 'deadline',
    });
  });
});
