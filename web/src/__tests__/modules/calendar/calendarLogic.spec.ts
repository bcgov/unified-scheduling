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
  buildCalendarAssignmentViewModel,
  buildCalendarSchedulingViewModel,
  getCalendarEventDateKey,
} from '@/modules/scheduling/calendarSchedulingMappers';
import type { CalendarSchedulingUserResource } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import type { CalendarSchedulingAssignmentResource } from '@/modules/scheduling/contributions/calendarSchedulingAssignmentsContribution';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import { calendarSchedulingActionIds } from '@/modules/scheduling/calendarSchedulingActionIds';
import { selectCalendarEvents, selectContribution } from '@/modules/calendar/calendarSelectors';
import { mapApiCalendarEventToCalendarEventBase } from '@/modules/calendar/contributions/calendarEventMappers';
import { buildCalendarPeriodSelectOptions, DEFAULT_CALENDAR_PERIODS } from '@/modules/calendar/calendarPeriodOptions';
import { buildCalendarDefaultViewModel } from '@/modules/calendar/views/calendarViewModels';
import type {
  CalendarDataResponse,
  CalendarQueryContext,
  CalendarRuntimeContext,
} from '@/modules/calendar/calendarTypes';
import { mdiAlertCircle, mdiCalendarSync } from '@mdi/js';

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
  it('renders assignments with the same start time in ascending assignment id order', () => {
    const assignmentEvents: CalendarSchedulingEvent[] = [
      {
        id: 'scheduling.assignment-entry.211',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Later assignment id',
        start: '2025-01-13T10:00:00Z',
        end: '2025-01-13T12:00:00Z',
        metadata: {
          assignmentEntryId: '211',
          assignedUserIds: ['user-1'],
        },
      },
      {
        id: 'scheduling.assignment-entry.209',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Earlier assignment id',
        start: '2025-01-13T10:00:00Z',
        end: '2025-01-13T12:00:00Z',
        metadata: {
          assignmentEntryId: '209',
          assignedUserIds: ['user-1'],
        },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events: assignmentEvents,
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
      {
        startDate: '2025-01-13',
        endDate: '2025-01-20',
        filters: { timeZone: 'America/Vancouver' },
      },
      'week',
    );

    const cell = viewModel.cells.find(
      (candidate) => candidate.resourceId === 'user-1' && candidate.date === '2025-01-13',
    );

    expect(cell?.groups[0]?.events.map((item) => item.event.id)).toEqual([
      'scheduling.assignment-entry.209',
      'scheduling.assignment-entry.211',
    ]);
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
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
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
    expect(
      (viewModel.payload as { assignmentEvents: CalendarSchedulingEvent[] }).assignmentEvents[0]?.metadata
        .assignmentDefinitionId,
    ).toBe('20');
  });

  it('adds a warning action to every assignment event listed in a backend conflict', () => {
    const shiftEvents: CalendarSchedulingEvent[] = [
      {
        id: 'assignment-conflict-1',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'First conflicting assignment',
        start: '2025-01-13T10:00:00Z',
        end: '2025-01-13T12:00:00Z',
        metadata: {
          eventId: 101,
          assignmentEntryId: '201',
          assignedUserIds: ['user-1'],
        },
      },
      {
        id: 'assignment-conflict-2',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Second conflicting assignment',
        start: '2025-01-13T11:00:00Z',
        end: '2025-01-13T13:00:00Z',
        metadata: {
          eventId: 102,
          assignmentEntryId: '202',
          assignedUserIds: ['user-1'],
        },
      },
      {
        id: 'shift-normal',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Normal shift',
        start: '2025-01-14T09:00:00',
        end: '2025-01-14T17:00:00',
        resourceIds: ['user-1'],
        statusTypeCode: 'active',
        metadata: { userIds: ['user-1'], shiftEntryId: '200', shiftSeriesId: 100 },
      },
      {
        id: 'assignment-linked',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Court coverage',
        color: 'blue',
        start: '2025-01-14T10:00:00',
        end: '2025-01-14T12:00:00',
        resourceIds: [],
        metadata: {
          assignmentEntryId: '200',
          assignedShiftIds: ['200'],
          assignedUserIds: ['user-1'],
        },
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
          'calendar.events': {
            moduleId: 'calendar',
            contributionId: 'calendar.events',
            events: [],
            data: {
              conflicts: [
                {
                  id: 'conflict:101:102:user-1',
                  entry: {
                    eventId: 101,
                    eventTypeCode: 'assignment',
                    sourceModule: 'scheduling',
                    title: 'First conflicting assignment',
                    start: '2025-01-13T10:00:00Z',
                    end: '2025-01-13T12:00:00Z',
                  },
                  overlaps: {
                    eventId: 102,
                    eventTypeCode: 'assignment',
                    sourceModule: 'scheduling',
                    title: 'Second conflicting assignment',
                    start: '2025-01-13T11:00:00Z',
                    end: '2025-01-13T13:00:00Z',
                  },
                  resourceId: 'user-1',
                  overlapStart: '2025-01-13T11:00:00Z',
                  overlapEnd: '2025-01-13T12:00:00Z',
                  isOverridden: false,
                },
              ],
            },
          },
        },
      },
      { startDate: '2025-01-13', endDate: '2025-01-20', filters: {} },
      'week',
    );

    const headers = viewModel.cells.flatMap((cell) => cell.headers ?? []);

    expect(headers.find((header) => header.id === 'shift-normal')?.action).toBeUndefined();
    expect(headers.find((header) => header.id === 'shift-normal')?.info?.icons).toContainEqual({
      icon: mdiCalendarSync,
      ariaLabel: 'Part of a shift series',
      title: 'Part of a shift series',
    });
    const conflictCell = viewModel.cells.find((cell) => cell.resourceId === 'user-1' && cell.date === '2025-01-13');
    expect(conflictCell?.groups[0]?.events).toHaveLength(2);
    expect(conflictCell?.groups[0]?.events.map((item) => item.display?.action)).toEqual([
      {
        actionId: calendarSchedulingActionIds.showConflict,
        icon: mdiAlertCircle,
        ariaLabel: 'Show Conflict Details',
        type: 'button',
      },
      {
        actionId: calendarSchedulingActionIds.showConflict,
        icon: mdiAlertCircle,
        ariaLabel: 'Show Conflict Details',
        type: 'button',
      },
    ]);
    expect(conflictCell?.groups[0]?.events[0]?.conflicts?.[0]?.id).toBe('conflict:101:102:user-1');

    const jan14Cell = viewModel.cells.find((cell) => cell.resourceId === 'user-1' && cell.date === '2025-01-14');
    expect(jan14Cell?.groups[0]?.action).toBeUndefined();
    expect(jan14Cell?.groups[0]?.events.map((item) => item.event.id)).toEqual(['assignment-linked']);
    expect(jan14Cell?.groups[0]?.events[0]?.event.title).toBe('Court coverage');
    expect(jan14Cell?.groups[0]?.events[0]?.display?.color).toBe('#5F79B8');
    expect(jan14Cell?.groups[0]?.events[0]?.display?.status).toBe('active');
  });

  it('uses linked shift status to style or hide assignment event blocks', () => {
    const events: CalendarSchedulingEvent[] = [
      {
        id: 'shift-draft',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Draft shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        statusTypeCode: 'draft',
        metadata: { userIds: ['user-1'], shiftEntryId: '301' },
      },
      {
        id: 'shift-active',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Active shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        statusTypeCode: 'active',
        metadata: { userIds: ['user-1'], shiftEntryId: '302' },
      },
      {
        id: 'shift-cancelled',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Cancelled shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        statusTypeCode: 'cancelled',
        metadata: { userIds: ['user-1'], shiftEntryId: '303' },
      },
      {
        id: 'assignment-draft-shift',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Draft shift assignment',
        start: '2025-01-13T10:00:00',
        end: '2025-01-13T11:00:00',
        metadata: {
          assignmentEntryId: '401',
          assignedShiftIds: ['301'],
          assignedUserIds: ['user-1'],
        },
      },
      {
        id: 'assignment-active-shift',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Active shift assignment',
        start: '2025-01-13T11:00:00',
        end: '2025-01-13T12:00:00',
        metadata: {
          assignmentEntryId: '402',
          assignedShiftIds: ['302'],
          assignedUserIds: ['user-1'],
        },
      },
      {
        id: 'assignment-cancelled-shift',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Cancelled shift assignment',
        start: '2025-01-13T12:00:00',
        end: '2025-01-13T13:00:00',
        metadata: {
          assignmentEntryId: '403',
          assignedShiftIds: ['303'],
          assignedUserIds: ['user-1'],
        },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events,
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

    const jan13Cell = viewModel.cells.find((cell) => cell.resourceId === 'user-1' && cell.date === '2025-01-13');
    const assignmentItems = jan13Cell?.groups[0]?.events ?? [];

    expect(assignmentItems.map((item) => item.event.id)).toEqual(['assignment-draft-shift', 'assignment-active-shift']);
    expect(assignmentItems.find((item) => item.event.id === 'assignment-draft-shift')?.display?.status).toBe('draft');
    expect(assignmentItems.find((item) => item.event.id === 'assignment-active-shift')?.display?.status).toBe('active');
  });

  it('does not place linked assignments on shift users who are not assigned to the assignment link', () => {
    const events: CalendarSchedulingEvent[] = [
      {
        id: 'shift-two-users',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Two user shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1', 'user-2'],
        statusTypeCode: 'active',
        metadata: { userIds: ['user-1', 'user-2'], shiftEntryId: '225' },
      },
      {
        id: 'assignment-one-user',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Assigned to one user',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        metadata: {
          assignmentEntryId: '281',
          assignedShiftIds: ['225'],
          assignedUserIds: ['user-1'],
        },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events,
            resources: [
              {
                id: 'user-1',
                type: 'user',
                sourceModule: 'scheduling',
                label: 'Alex Alpha',
                title: 'Alex Alpha',
              },
              {
                id: 'user-2',
                type: 'user',
                sourceModule: 'scheduling',
                label: 'Blair Beta',
                title: 'Blair Beta',
              },
            ] as CalendarSchedulingUserResource[],
          },
        },
      },
      { startDate: '2025-01-13', endDate: '2025-01-20', filters: {} },
      'week',
    );

    const userOneCell = viewModel.cells.find((cell) => cell.resourceId === 'user-1' && cell.date === '2025-01-13');
    const userTwoCell = viewModel.cells.find((cell) => cell.resourceId === 'user-2' && cell.date === '2025-01-13');

    expect(userOneCell?.groups[0]?.events.map((item) => item.event.id)).toEqual(['assignment-one-user']);
    expect(userTwoCell?.groups[0]?.events.map((item) => item.event.id)).toEqual([]);
  });

  it('falls back to linked shifts when assignment assigned users are empty', () => {
    const events: CalendarSchedulingEvent[] = [
      {
        id: 'shift-user-one',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'User one shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        statusTypeCode: 'active',
        metadata: { userIds: ['user-1'], shiftEntryId: '225' },
      },
      {
        id: 'assignment-linked-empty-users',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Linked assignment',
        start: '2025-01-13T10:00:00',
        end: '2025-01-13T11:00:00',
        metadata: {
          assignmentEntryId: '281',
          assignedShiftIds: ['225'],
          assignedUserIds: [],
        },
      },
      {
        id: 'assignment-unmatched-empty-users',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Unmatched assignment',
        start: '2025-01-13T11:00:00',
        end: '2025-01-13T12:00:00',
        metadata: {
          assignmentEntryId: '282',
          assignedShiftIds: ['999'],
          assignedUserIds: [],
        },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events,
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

    const userCell = viewModel.cells.find((cell) => cell.resourceId === 'user-1' && cell.date === '2025-01-13');

    expect(userCell?.groups[0]?.events.map((item) => item.event.id)).toEqual(['assignment-linked-empty-users']);
  });

  it('adds an unassigned row for shifts without users and assignments without linked shifts', () => {
    const events: CalendarSchedulingEvent[] = [
      {
        id: 'shift-assigned',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Assigned shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        metadata: { userIds: ['user-1'], shiftEntryId: '501' },
      },
      {
        id: 'shift-unassigned',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Unassigned shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: [],
        metadata: { userIds: [], shiftEntryId: '502' },
      },
      {
        id: 'assignment-unlinked',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Unlinked assignment',
        start: '2025-01-13T10:00:00',
        end: '2025-01-13T11:00:00',
        metadata: {
          assignmentEntryId: '601',
          assignedShiftIds: [],
          assignedUserIds: [],
        },
      },
      {
        id: 'assignment-linked',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Linked assignment',
        start: '2025-01-13T11:00:00',
        end: '2025-01-13T12:00:00',
        metadata: {
          assignmentEntryId: '602',
          assignedShiftIds: ['501'],
          assignedUserIds: ['user-1'],
        },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events,
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

    expect(viewModel.primaryColumn.resources.map((resource) => resource.id)).toEqual([
      'user-1',
      'scheduling.unassigned',
    ]);

    const assignedCell = viewModel.cells.find((cell) => cell.resourceId === 'user-1' && cell.date === '2025-01-13');
    expect(assignedCell?.headers?.map((header) => header.id)).toEqual(['shift-assigned']);
    expect(assignedCell?.groups[0]?.events.map((item) => item.event.id)).toEqual(['assignment-linked']);

    const unassignedCell = viewModel.cells.find(
      (cell) => cell.resourceId === 'scheduling.unassigned' && cell.date === '2025-01-13',
    );
    expect(unassignedCell?.headers?.map((header) => header.id)).toEqual(['shift-unassigned']);
    expect(unassignedCell?.groups[0]?.events.map((item) => item.event.id)).toEqual(['assignment-unlinked']);
  });

  it('does not add an unassigned row when all shifts and assignments are assigned', () => {
    const events: CalendarSchedulingEvent[] = [
      {
        id: 'shift-assigned',
        type: 'scheduling.shift',
        sourceModule: 'calendar-scheduling',
        title: 'Assigned shift',
        start: '2025-01-13T09:00:00',
        end: '2025-01-13T17:00:00',
        resourceIds: ['user-1'],
        metadata: { userIds: ['user-1'], shiftEntryId: '501' },
      },
      {
        id: 'assignment-linked',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'Linked assignment',
        start: '2025-01-13T11:00:00',
        end: '2025-01-13T12:00:00',
        metadata: {
          assignmentEntryId: '602',
          assignedShiftIds: ['501'],
          assignedUserIds: ['user-1'],
        },
      },
    ];

    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events,
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

    expect(viewModel.primaryColumn.resources.map((resource) => resource.id)).toEqual(['user-1']);
    expect(viewModel.cells.some((cell) => cell.resourceId === 'scheduling.unassigned')).toBe(false);
  });

  it('builds assignment view rows from assignment definitions and places assignments in the matching row', () => {
    const viewModel = buildCalendarAssignmentViewModel(
      {
        contributions: {
          'scheduling.assignment-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.assignment-events',
            events: [
              {
                id: 'assignment-entry-700',
                type: 'scheduling.assignment',
                sourceModule: 'calendar-assignment',
                title: 'Court coverage',
                start: '2025-01-13T09:00:00',
                end: '2025-01-13T17:00:00',
                metadata: {
                  assignmentDefinitionId: '20',
                  assignmentEntryId: '700',
                },
              },
            ] as CalendarSchedulingEvent[],
            resources: [
              {
                id: 'assignment-definition-20',
                type: 'assignment',
                sourceModule: 'scheduling',
                label: 'Courtroom',
                title: 'Courtroom',
                assignmentDefinitionId: 20,
              },
              {
                id: 'assignment-definition-30',
                type: 'assignment',
                sourceModule: 'scheduling',
                label: 'Registry',
                title: 'Registry',
                assignmentDefinitionId: 30,
              },
            ] as CalendarSchedulingAssignmentResource[],
          },
        },
      },
      { startDate: '2025-01-13', endDate: '2025-01-20', filters: {} },
      'week',
    );

    expect(viewModel.primaryColumn.resources.map((resource) => resource.id)).toEqual([
      'assignment-definition-20',
      'assignment-definition-30',
    ]);
    expect(viewModel.primaryColumn.resources[0]?.action?.actionId).toBe(
      calendarSchedulingActionIds.addAssignmentResource,
    );
    expect(viewModel.sidePanel?.actionId).toBe(calendarSchedulingActionIds.scheduleStaff);

    const courtroomCell = viewModel.cells.find(
      (cell) => cell.resourceId === 'assignment-definition-20' && cell.date === '2025-01-13',
    );
    const registryCell = viewModel.cells.find(
      (cell) => cell.resourceId === 'assignment-definition-30' && cell.date === '2025-01-13',
    );

    expect(courtroomCell?.groups[0]?.events.map((item) => item.event.id)).toEqual(['assignment-entry-700']);
    expect(courtroomCell?.groups[0]?.action).toBeUndefined();
    expect(registryCell?.groups[0]?.events).toEqual([]);
  });

  it('places assignment events from the shared scheduling contribution in the matching assignment row', () => {
    const viewModel = buildCalendarAssignmentViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events: [
              {
                id: 'scheduling.shift-entry.200',
                type: 'scheduling.shift',
                sourceModule: 'scheduling',
                title: 'Alex Alpha shift',
                start: '2026-07-13T16:00:00+00:00',
                end: '2026-07-14T00:00:00+00:00',
                timeZoneId: 'America/Vancouver',
                eventTypeCode: 'shift',
                statusTypeCode: 'draft',
                resourceIds: ['user-1'],
                metadata: {
                  shiftEntryId: '200',
                  userIds: ['user-1'],
                },
              },
              {
                id: 'scheduling.assignment-entry.257',
                type: 'scheduling.assignment',
                sourceModule: 'scheduling',
                title: 'Yellow Assignment',
                color: 'yellow',
                start: '2026-07-13T16:00:00+00:00',
                end: '2026-07-14T00:00:00+00:00',
                timeZoneId: 'America/Vancouver',
                resourceIds: [],
                metadata: {
                  assignmentEntryId: '257',
                  assignmentSeriesId: '211',
                  assignmentCategoryTypeId: 200,
                  assignmentSubCategoryTypeId: 201,
                  assignedShiftIds: ['200'],
                  assignedUserIds: ['user-1'],
                },
              },
            ] as CalendarSchedulingEvent[],
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
          'scheduling.assignment-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.assignment-events',
            events: [],
            resources: [
              {
                id: 'assignment-definition-4',
                type: 'assignment',
                sourceModule: 'scheduling',
                label: 'Yellow Assignment',
                title: 'Yellow Assignment',
                assignmentDefinitionId: 4,
                assignmentCategoryTypeId: 200,
                assignmentSubCategoryTypeId: 201,
              },
            ] as CalendarSchedulingAssignmentResource[],
          },
        },
      },
      { startDate: '2026-07-13', endDate: '2026-07-20', filters: { timeZoneId: 'America/Vancouver' } },
      'week',
    );

    const yellowAssignmentCell = viewModel.cells.find(
      (cell) => cell.resourceId === 'assignment-definition-4' && cell.date === '2026-07-13',
    );

    expect(yellowAssignmentCell?.groups[0]?.events.map((item) => item.event.id)).toEqual([
      'scheduling.assignment-entry.257',
    ]);
    expect(
      (
        (yellowAssignmentCell?.payload as { shiftEvents?: CalendarSchedulingEvent[] } | undefined)?.shiftEvents ?? []
      ).map((event) => event.id),
    ).toEqual(['scheduling.shift-entry.200']);
  });

  it('marks assignment view capacity slots as partial when the linked shift does not fully match assignment time', () => {
    const viewModel = buildCalendarAssignmentViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events: [
              {
                id: 'scheduling.shift-entry.200',
                type: 'scheduling.shift',
                sourceModule: 'scheduling',
                title: 'Alex Alpha shift',
                start: '2026-07-13T16:00:00+00:00',
                end: '2026-07-14T00:00:00+00:00',
                timeZoneId: 'America/Vancouver',
                eventTypeCode: 'shift',
                statusTypeCode: 'active',
                resourceIds: ['user-1', 'user-2'],
                metadata: {
                  shiftEntryId: '200',
                  userIds: ['user-1', 'user-2'],
                },
              },
              {
                id: 'scheduling.assignment-entry.257',
                type: 'scheduling.assignment',
                sourceModule: 'scheduling',
                title: 'Yellow Assignment',
                color: 'yellow',
                start: '2026-07-13T15:30:00+00:00',
                end: '2026-07-14T00:00:00+00:00',
                timeZoneId: 'America/Vancouver',
                resourceIds: [],
                metadata: {
                  assignmentDefinitionId: '4',
                  assignmentEntryId: '257',
                  assignmentCategoryTypeId: 200,
                  assignmentSubCategoryTypeId: 201,
                  capacity: 1,
                  assignedCount: 1,
                  assignedShiftIds: ['200'],
                  assignedUserIds: ['user-1'],
                },
              },
            ] as CalendarSchedulingEvent[],
            resources: [
              {
                id: 'user-1',
                type: 'user',
                sourceModule: 'scheduling',
                label: 'Alex Alpha',
                title: 'Alex Alpha',
              },
              {
                id: 'user-2',
                type: 'user',
                sourceModule: 'scheduling',
                label: 'Blair Beta',
                title: 'Blair Beta',
              },
            ] as CalendarSchedulingUserResource[],
          },
          'scheduling.assignment-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.assignment-events',
            events: [],
            resources: [
              {
                id: 'assignment-definition-4',
                type: 'assignment',
                sourceModule: 'scheduling',
                label: 'Yellow Assignment',
                title: 'Yellow Assignment',
                assignmentDefinitionId: 4,
                assignmentCategoryTypeId: 200,
                assignmentSubCategoryTypeId: 201,
              },
            ] as CalendarSchedulingAssignmentResource[],
          },
        },
      },
      { startDate: '2026-07-13', endDate: '2026-07-20', filters: { timeZoneId: 'America/Vancouver' } },
      'week',
    );

    const yellowAssignmentCell = viewModel.cells.find(
      (cell) => cell.resourceId === 'assignment-definition-4' && cell.date === '2026-07-13',
    );
    const assignmentEvent = yellowAssignmentCell?.groups[0]?.events[0]?.event as CalendarSchedulingEvent | undefined;

    expect(assignmentEvent?.metadata.capacitySlotStates).toEqual(['partial']);
    expect(assignmentEvent?.metadata.partialCoverageShifts).toEqual([
      {
        userIds: ['user-1'],
        start: '2026-07-13T16:00:00+00:00',
        end: '2026-07-14T00:00:00+00:00',
        timeZoneId: 'America/Vancouver',
      },
    ]);
  });

  it('does not mark assignment coverage as partial when the linked shift fully covers assignment time', () => {
    const viewModel = buildCalendarAssignmentViewModel(
      {
        contributions: {
          'scheduling.shift-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.shift-events',
            events: [
              {
                id: 'scheduling.shift-entry.200',
                type: 'scheduling.shift',
                sourceModule: 'scheduling',
                title: 'Alex Alpha shift',
                start: '2026-07-13T16:00:00+00:00',
                end: '2026-07-14T00:00:00+00:00',
                timeZoneId: 'America/Vancouver',
                eventTypeCode: 'shift',
                statusTypeCode: 'active',
                resourceIds: ['user-1'],
                metadata: {
                  shiftEntryId: '200',
                  userIds: ['user-1'],
                },
              },
              {
                id: 'scheduling.assignment-entry.257',
                type: 'scheduling.assignment',
                sourceModule: 'scheduling',
                title: 'Yellow Assignment',
                color: 'yellow',
                start: '2026-07-13T16:00:00+00:00',
                end: '2026-07-13T17:30:00+00:00',
                timeZoneId: 'America/Vancouver',
                resourceIds: [],
                metadata: {
                  assignmentDefinitionId: '4',
                  assignmentEntryId: '257',
                  assignmentCategoryTypeId: 200,
                  assignmentSubCategoryTypeId: 201,
                  capacity: 1,
                  assignedCount: 1,
                  assignedShiftIds: ['200'],
                  assignedUserIds: ['user-1'],
                },
              },
            ] as CalendarSchedulingEvent[],
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
          'scheduling.assignment-events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.assignment-events',
            events: [],
            resources: [
              {
                id: 'assignment-definition-4',
                type: 'assignment',
                sourceModule: 'scheduling',
                label: 'Yellow Assignment',
                title: 'Yellow Assignment',
                assignmentDefinitionId: 4,
                assignmentCategoryTypeId: 200,
                assignmentSubCategoryTypeId: 201,
              },
            ] as CalendarSchedulingAssignmentResource[],
          },
        },
      },
      { startDate: '2026-07-13', endDate: '2026-07-20', filters: { timeZoneId: 'America/Vancouver' } },
      'week',
    );

    const yellowAssignmentCell = viewModel.cells.find(
      (cell) => cell.resourceId === 'assignment-definition-4' && cell.date === '2026-07-13',
    );
    const assignmentEvent = yellowAssignmentCell?.groups[0]?.events[0]?.event as CalendarSchedulingEvent | undefined;

    expect(assignmentEvent?.metadata.capacitySlotStates).toEqual(['filled']);
    expect(assignmentEvent?.metadata.partialCoverageShifts).toEqual([]);
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
