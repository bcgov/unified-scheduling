import { describe, expect, it } from 'vitest';
import { buildCalendarDefaultViewModel } from '@/modules/calendar/views/calendarViewModels';
import {
  buildCalendarAssignmentViewModel,
  buildCalendarSchedulingViewModel,
} from '@/modules/scheduling/calendarSchedulingMappers';
import type { CalendarDataResponse } from '@/modules/calendar/calendarTypes';
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
    expect(cells[0]?.headers?.[0]).toMatchObject({ text: 'Training: FIRST-AID', payload: training });
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
