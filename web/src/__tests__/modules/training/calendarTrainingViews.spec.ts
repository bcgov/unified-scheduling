import { describe, expect, it } from 'vitest';
import { buildCalendarDefaultViewModel } from '@/modules/calendar/views/calendarViewModels';
import {
  buildCalendarAssignmentViewModel,
  buildCalendarSchedulingViewModel,
} from '@/modules/scheduling/calendarSchedulingMappers';
import type { CalendarConflict, CalendarDataResponse } from '@/modules/calendar/calendarTypes';
import type { CalendarSchedulingUserResource } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';

const training = {
  id: 'training.user-training.42',
  type: 'training.user-training',
  sourceModule: 'training',
  title: 'Training: FIRST-AID',
  start: '2025-04-08',
  end: '2025-04-11',
  allDay: true,
  isReadOnly: true,
  resourceIds: ['user-1'],
};
const query = { startDate: '2025-04-07', endDate: '2025-04-14', filters: {} };
const response: CalendarDataResponse = {
  contributions: {
    'training.events': { moduleId: 'training', contributionId: 'training.events', events: [training] },
    'scheduling.events': {
      moduleId: 'scheduling',
      contributionId: 'scheduling.events',
      events: [],
      resources: [
        { id: 'user-1', type: 'user', sourceModule: 'scheduling', label: 'One', title: 'One' },
        { id: 'user-2', type: 'user', sourceModule: 'scheduling', label: 'Two', title: 'Two' },
      ] as CalendarSchedulingUserResource[],
    },
  },
};

describe('training calendar views', () => {
  it('includes training in the default Calendar aggregation', () => {
    const model = buildCalendarDefaultViewModel(response, query, { featureFlags: {} }, 'week');
    expect(model.events).toContainEqual(training);
  });

  it('places multi-day training in its user row through the final inclusive day', () => {
    const model = buildCalendarSchedulingViewModel(response, query, 'week');
    const cells = model.cells.filter((cell) => cell.headers?.some((header) => header.id === training.id));
    expect(cells.map((cell) => [cell.resourceId, cell.date])).toEqual([
      ['user-1', '2025-04-08'],
      ['user-1', '2025-04-09'],
      ['user-1', '2025-04-10'],
    ]);
    expect(cells[0]?.headers?.[0]).toMatchObject({
      text: 'Training: FIRST-AID',
      subtitle: 'All Day',
      payload: training,
    });
    expect(cells[0]?.headers?.[0]?.actionId).toBe('training.view-details');
    expect(model.cells.filter((cell) => cell.resourceId === 'user-2').flatMap((cell) => cell.headers ?? [])).toEqual(
      [],
    );
  });

  it('keeps training out of the assignment resource view', () => {
    const model = buildCalendarAssignmentViewModel(response, query, 'week');
    expect(model.cells.flatMap((cell) => cell.groups.flatMap((group) => group.events))).toEqual([]);
  });

  it.each([
    ['2025-04-08T09:00:00-07:00', '2025-04-08T10:00:00-07:00', ['2025-04-08']],
    ['2025-04-09T06:30:00Z', '2025-04-09T07:30:00Z', ['2025-04-08', '2025-04-09']],
    ['2025-04-08T23:00:00-07:00', '2025-04-09T00:00:00-07:00', ['2025-04-08']],
    ['2025-04-09T00:00:00+02:00', '2025-04-09T01:00:00+02:00', ['2025-04-08']],
  ])('places timed training from %s to %s only on overlapping local days', (start, end, dates) => {
    const timedTraining = { ...training, start, end, allDay: false };
    const model = buildCalendarSchedulingViewModel(
      {
        contributions: {
          ...response.contributions,
          'training.events': { moduleId: 'training', contributionId: 'training.events', events: [timedTraining] },
        },
      },
      { ...query, filters: { timeZoneId: 'America/Vancouver' } },
      'week',
    );

    const cells = model.cells.filter((cell) => cell.headers?.some((header) => header.id === training.id));
    expect(cells.map((cell) => [cell.resourceId, cell.date])).toEqual(dates.map((date) => ['user-1', date]));
    for (const cell of cells) {
      expect(cell.headers?.[0]).toMatchObject({
        allDay: false,
        actionId: 'training.view-details',
        payload: timedTraining,
      });
      expect(cell.headers?.[0]?.subtitle).toBeUndefined();
      expect(cell.headers?.[0]?.text).toMatch(/^Training: FIRST-AID \d{1,2}:\d{2} [AP]M - \d{1,2}:\d{2} [AP]M$/);
    }
    if (start === '2025-04-08T09:00:00-07:00') {
      expect(cells[0]?.headers?.[0]?.text).toBe('Training: FIRST-AID 9:00 AM - 10:00 AM');
    }
  });

  it.each([
    ['2025-03-03', '2025-03-09T01:30:00-08:00', '2025-03-09T03:30:00-07:00', '2025-03-09'],
    ['2025-10-27', '2025-11-02T01:30:00-07:00', '2025-11-02T01:30:00-08:00', '2025-11-02'],
  ])('keeps training on its local day across DST in the week of %s', (startDate, start, end, date) => {
    const model = buildCalendarSchedulingViewModel(
      {
        contributions: {
          ...response.contributions,
          'training.events': {
            moduleId: 'training',
            contributionId: 'training.events',
            events: [{ ...training, start, end, allDay: false }],
          },
        },
      },
      { ...query, startDate, filters: { timeZoneId: 'America/Vancouver' } },
      'week',
    );

    expect(model.cells.filter((cell) => cell.headers?.length).map((cell) => [cell.resourceId, cell.date])).toEqual([
      ['user-1', date],
    ]);
  });

  it.each([true, false])('adds restricted warning controls to training headers (allDay: %s)', (allDay) => {
    const event = allDay
      ? training
      : { ...training, start: '2025-04-08T09:00:00-07:00', end: '2025-04-10T10:00:00-07:00', allDay };
    const warning = {
      sourceModule: 'training',
      eventId: training.id,
      resourceId: 'user-1',
      message: 'This event has a conflict with an event you do not have permission to view.',
    };
    const model = buildCalendarSchedulingViewModel(
      {
        contributions: {
          ...response.contributions,
          'training.events': { moduleId: 'training', contributionId: 'training.events', events: [event] },
          'calendar.events': {
            moduleId: 'calendar',
            contributionId: 'calendar.events',
            events: [],
            data: { conflicts: [], restrictedWarnings: [warning, { ...warning, resourceId: 'user-2' }] },
          },
        },
      },
      query,
      'week',
      false,
    );

    const headers = model.cells.flatMap((cell) => cell.headers ?? []);
    expect(headers).toHaveLength(3);
    for (const header of headers) {
      expect(header.payload).toEqual(event);
      expect(header.restrictedWarnings).toEqual([warning]);
      expect(header.action?.actionId).toBe('calendar-scheduling.show-conflict');
      expect(header.actionId).toBe('training.view-details');
      expect(header.conflicts).toEqual([]);
    }
  });

  it.each([
    ['calendar', true, false],
    ['calendar', false, true],
    ['training', true, false],
    ['training', false, true],
    ['training', true, true],
    ['training', false, false],
  ] as const)(
    'attaches %s conflicts only to reportable training headers (%s, %s)',
    (otherModule, reportOnEntry, reportOnOverlaps) => {
      const secondTraining = { ...training, id: 'training.user-training.43', title: 'Training: CPR' };
      const conflict: CalendarConflict = {
        id: 'training-conflict',
        entry: {
          eventId: training.id,
          sourceModule: 'training',
          title: training.title,
          start: '2025-04-08T07:00:00Z',
          end: '2025-04-11T07:00:00Z',
          sourceEntityId: 42,
          timeZoneId: 'America/Vancouver',
          allDay: true,
        },
        overlaps: {
          eventId: otherModule === 'training' ? secondTraining.id : '42',
          sourceModule: otherModule,
          title: otherModule === 'training' ? secondTraining.title : 'Holiday',
          start: '2025-04-09T07:00:00Z',
          end: '2025-04-10T07:00:00Z',
          sourceEntityId: null,
          timeZoneId: 'America/Vancouver',
          allDay: true,
        },
        resourceId: 'user-1',
        overlapStart: '2025-04-09T07:00:00Z',
        overlapEnd: '2025-04-10T07:00:00Z',
        reportOnEntry,
        reportOnOverlaps,
        isOverridden: false,
        overrideNote: null,
        createdById: null,
        createdOn: null,
        updatedById: null,
        updatedOn: null,
      };
      const otherUserConflict = { ...conflict, id: 'other-user-conflict', resourceId: 'user-2' };
      const model = buildCalendarSchedulingViewModel(
        {
          contributions: {
            ...response.contributions,
            'training.events': {
              moduleId: 'training',
              contributionId: 'training.events',
              events: [training, secondTraining],
            },
            'calendar.events': {
              moduleId: 'calendar',
              contributionId: 'calendar.events',
              events: [],
              data: { conflicts: [conflict, otherUserConflict] },
            },
          },
        },
        { ...query, filters: { timeZoneId: 'America/Vancouver' } },
        'week',
      );

      for (const cell of model.cells) {
        for (const header of cell.headers ?? []) {
          const reportable =
            cell.resourceId === 'user-1' &&
            cell.date === '2025-04-09' &&
            (header.id === training.id ? reportOnEntry : otherModule === 'training' && reportOnOverlaps);
          expect(header.conflicts).toEqual(
            reportable ? [{ conflict, currentEventId: { sourceModule: 'training', eventId: header.id } }] : [],
          );
          expect(header.action?.actionId).toBe(reportable ? 'calendar-scheduling.show-conflict' : undefined);
          expect(header.actionId).toBe('training.view-details');
        }
      }
    },
  );

  it.each([
    ['2025-04-09T07:00:00Z', '2025-04-10T07:00:00Z', ['2025-04-09']],
    ['2025-04-09T23:00:00Z', '2025-04-11T07:00:00Z', ['2025-04-09', '2025-04-10']],
  ])(
    'limits borrowed training to loan-out dates from %s to %s and retains home training',
    (startAtUtc, endAtUtc, dates) => {
      const borrowedTraining = {
        ...training,
        id: 'training.user-training.43',
        start: query.startDate,
        end: query.endDate,
        resourceIds: ['borrowed-user'],
      };
      const resources: CalendarSchedulingUserResource[] = ['borrowed-user', 'user-1'].map((id) => ({
        id,
        type: 'user',
        sourceModule: 'loanout',
        label: id,
        title: id,
        loanOutMetaData: [{ loanOutLocationId: 12, startAtUtc, endAtUtc }],
      }));
      const model = buildCalendarSchedulingViewModel(
        {
          contributions: {
            ...response.contributions,
            'training.events': {
              moduleId: 'training',
              contributionId: 'training.events',
              events: [training, borrowedTraining],
            },
            'scheduling.loanout': {
              moduleId: 'loanout',
              contributionId: 'scheduling.loanout',
              events: [],
              resources,
            },
          },
        },
        { ...query, locationId: 12, filters: { timeZoneId: 'America/Vancouver' } },
        'week',
      );

      const borrowedCells = model.cells.filter((cell) =>
        cell.headers?.some((header) => header.id === borrowedTraining.id),
      );
      expect(borrowedCells.map((cell) => [cell.resourceId, cell.date])).toEqual(
        dates.map((date) => ['borrowed-user', date]),
      );
      expect(borrowedCells.every((cell) => !cell.disabled)).toBe(true);
      expect(
        model.cells
          .filter((cell) => cell.headers?.some((header) => header.id === training.id))
          .map((cell) => cell.date),
      ).toEqual(['2025-04-08', '2025-04-09', '2025-04-10']);
    },
  );
});
