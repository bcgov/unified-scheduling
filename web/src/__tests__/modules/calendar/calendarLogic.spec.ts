import { describe, expect, it } from 'vitest';

import {
  addDays,
  buildDateRangeForPeriod,
  formatCalendarDateOnly,
  formatLocalDateOnly,
  formatRangeLabel,
  localDateOnlyToUtcInstant,
  parseLocalDateOnly,
  shiftCalendarAnchor,
  startOfMonth,
  startOfWeek,
  toCalendarDateOnly,
} from '@/utils/date';
import {
  CalendarEventStatusTypeCode,
  CalendarEventType,
  CalendarEventTypeCode,
  type CalendarEventResponse,
} from '@/api-access/generated/models';
import {
  buildCalendarAssignmentViewModel,
  buildCalendarSchedulingViewModel,
  getCalendarEventDateKey,
} from '@/modules/scheduling/calendarSchedulingMappers';
import type { CalendarSchedulingUserResource } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import type { CalendarSchedulingAssignmentResource } from '@/modules/scheduling/contributions/calendarSchedulingAssignmentsContribution';
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
  it('builds ranges for every period and shifts anchors correctly', () => {
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

    expect(shiftCalendarAnchor('2025-01-15', 'day', 'previous')).toBe('2025-01-14');
    expect(shiftCalendarAnchor('2025-01-15', 'week', 'next')).toBe('2025-01-22');
    expect(shiftCalendarAnchor('2025-01-15', 'work-week', 'next')).toBe('2025-01-22');
    expect(shiftCalendarAnchor('2025-01-31', 'month', 'next')).toBe('2025-02-28');
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
  it('shows user-specific conflicts in assignment resource rows', () => {
    const assignmentEvent: CalendarSchedulingEvent = {
      id: 'scheduling.assignment-entry.90',
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Court Room Monitor',
      start: '2025-01-13T17:00:00Z',
      end: '2025-01-13T18:00:00Z',
      metadata: {
        eventId: 900,
        assignmentEntryId: '90',
        assignmentDefinitionId: '20',
      },
    };
    const viewModel = buildCalendarAssignmentViewModel(
      {
        contributions: {
          'calendar.events': {
            moduleId: 'calendar',
            contributionId: 'calendar.events',
            events: [],
            data: {
              conflicts: [
                {
                  id: '900:901:user-1',
                  resourceId: 'user-1',
                  entry: {
                    eventId: 900,
                    eventTypeCode: 'assignment',
                    sourceModule: 'scheduling',
                    title: 'Court Room Monitor',
                    start: '2025-01-13T17:00:00Z',
                    end: '2025-01-13T18:00:00Z',
                    sourceEntityId: null,
                    timeZoneId: null,
                  },
                  overlaps: {
                    eventId: 901,
                    eventTypeCode: 'assignment',
                    sourceModule: 'scheduling',
                    title: 'Other assignment',
                    start: '2025-01-13T17:30:00Z',
                    end: '2025-01-13T18:30:00Z',
                    sourceEntityId: null,
                    timeZoneId: null,
                  },
                  overlapStart: '2025-01-13T17:30:00Z',
                  overlapEnd: '2025-01-13T18:00:00Z',
                  isOverridden: false,
                },
              ],
            },
          },
          'scheduling.assignment-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.assignment-events',
            events: [assignmentEvent],
            resources: [
              {
                id: 'assignment-definition-20',
                type: 'assignment',
                sourceModule: 'scheduling',
                label: 'Court Room Monitor',
                title: 'Court Room Monitor',
                assignmentDefinitionId: 20,
              },
            ] as CalendarSchedulingAssignmentResource[],
          },
        },
      },
      { startDate: '2025-01-13', endDate: '2025-01-20', filters: {} },
      'week',
    );

    const item = viewModel.cells.find(
      (cell) => cell.resourceId === 'assignment-definition-20' && cell.date === '2025-01-13',
    )?.groups[0]?.events[0];

    expect(item?.conflicts).toHaveLength(1);
    expect(item?.display?.action?.ariaLabel).toBe('Show conflict');
  });

  it('enriches a shift-contribution assignment with its definition before cross-user drop matching', () => {
    const assignmentEvent: CalendarSchedulingEvent = {
      id: 'scheduling.assignment-entry.90',
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Court Room Monitor',
      start: '2025-01-13T17:00:00Z',
      end: '2025-01-13T18:00:00Z',
      metadata: {
        assignmentEntryId: '90',
        assignedUserIds: ['user-2'],
        assignedShiftIds: ['44'],
      },
    };
    const users = [
      {
        id: 'user-1',
        type: 'user',
        sourceModule: 'scheduling',
        label: 'Target User',
        title: 'Target User',
      },
      {
        id: 'user-2',
        type: 'user',
        sourceModule: 'scheduling',
        label: 'Other User',
        title: 'Other User',
      },
    ] as CalendarSchedulingUserResource[];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.events',
            events: [assignmentEvent],
            resources: users,
          },
          'scheduling.assignment-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.assignment-events',
            events: [],
            resources: [
              {
                id: 'assignment-definition-20',
                type: 'assignment',
                sourceModule: 'scheduling',
                label: 'Court Room Monitor',
                title: 'Court Room Monitor',
                assignmentDefinitionId: 20,
              },
            ] as CalendarSchedulingAssignmentResource[],
          },
        },
      },
      {
        startDate: '2025-01-13',
        endDate: '2025-01-20',
        filters: { timeZone: 'America/Vancouver' },
      },
      'week',
    );

    const targetCell = viewModel.cells.find((cell) => cell.resourceId === 'user-1' && cell.date === '2025-01-13');
    const otherUserCell = viewModel.cells.find((cell) => cell.resourceId === 'user-2' && cell.date === '2025-01-13');

    expect(targetCell?.headers).toEqual([]);
    expect(targetCell?.groups[0]?.events).toEqual([]);
    expect(otherUserCell?.groups[0]?.events.map((item) => item.event.id)).toEqual(['scheduling.assignment-entry.90']);
  });

  it('shows the conflict action only when a shift has a conflict for that user', () => {
    const shiftEvents: CalendarSchedulingEvent[] = [
      {
        id: 'shift-conflict',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Conflict shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        metadata: { eventId: 101, userIds: ['user-1'] },
      },
      {
        id: 'shift-normal',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Normal shift',
        start: '2025-01-14T09:00:00',
        end: '2025-01-14T17:00:00',
        resourceIds: ['user-1'],
        metadata: { eventId: 102, userIds: ['user-1'], shiftSeriesId: 100 },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'calendar.events': {
            moduleId: 'calendar',
            contributionId: 'calendar.events',
            events: [],
            data: {
              conflicts: [
                {
                  id: '101:201:user-1',
                  resourceId: 'user-1',
                  entry: {
                    eventId: 101,
                    eventTypeCode: 'shift',
                    sourceModule: 'scheduling',
                    title: 'Conflict shift',
                    start: '2025-01-13T09:00:00Z',
                    end: '2025-01-13T17:00:00Z',
                    sourceEntityId: null,
                    timeZoneId: null,
                  },
                  overlaps: {
                    eventId: 201,
                    eventTypeCode: 'assignment',
                    sourceModule: 'scheduling',
                    title: 'Assignment',
                    start: '2025-01-13T10:00:00Z',
                    end: '2025-01-13T12:00:00Z',
                    sourceEntityId: null,
                    timeZoneId: null,
                  },
                  overlapStart: '2025-01-13T10:00:00Z',
                  overlapEnd: '2025-01-13T12:00:00Z',
                  isOverridden: false,
                },
              ],
            },
          },
          'scheduling.events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.events',
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

    expect(headers.find((header) => header.id === 'shift-conflict')?.action?.ariaLabel).toBe('Show Conflict Details');
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
        id: '10',
        title: 'Holiday',
        startAtUtc: '2025-07-01T00:00:00Z',
        endAtUtc: '2025-07-02T00:00:00Z',
        allDay: true,
        isReadOnly: true,
        isException: false,
        holidayType: 'CanadaDay',
        eventTypeCode: '',
        statusTypeCode: CalendarEventStatusTypeCode.Active,
        sourceModule: 'calendar',
      } as unknown as CalendarEventResponse),
    ).toMatchObject({
      id: '10',
      type: CalendarEventType.calendarevent,
      start: '2025-07-01',
      end: '2025-07-02',
      eventTypeCode: CalendarEventTypeCode.General,
      statusTypeCode: CalendarEventStatusTypeCode.Active,
      isReadOnly: true,
      holidayType: 'CanadaDay',
    });
  });

  it('preserves timestamp values for non all-day events', () => {
    expect(
      mapApiCalendarEventToCalendarEventBase({
        id: '11',
        title: 'Meeting',
        startAtUtc: '2025-07-01T09:00:00Z',
        endAtUtc: '2025-07-01T10:00:00Z',
        allDay: false,
        isReadOnly: false,
        isException: true,
        type: CalendarEventType.calendarevent,
        eventTypeCode: CalendarEventTypeCode.Deadline,
        statusTypeCode: CalendarEventStatusTypeCode.Draft,
        sourceModule: 'calendar',
      }),
    ).toMatchObject({
      id: '11',
      type: CalendarEventType.calendarevent,
      start: '2025-07-01T09:00:00Z',
      end: '2025-07-01T10:00:00Z',
      isException: true,
      eventTypeCode: CalendarEventTypeCode.Deadline,
    });
  });
});
