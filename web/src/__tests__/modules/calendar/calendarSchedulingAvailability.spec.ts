import { describe, expect, it } from 'vitest';
import {
  isAvailableForSchedule,
  buildCalendarSchedulingViewModel,
} from '@/modules/scheduling/calendarSchedulingMappers';
import { createPinia, setActivePinia } from 'pinia';
import { useUsersStore } from '@/stores/Users';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';

describe('isAvailableForSchedule', () => {
  it.each([
    [['selected-user'], ['selected-user']],
    [undefined, ['selected-user', 'other-user']],
    [[], ['selected-user', 'other-user']],
    [
      ['', ' '],
      ['selected-user', 'other-user'],
    ],
  ])('respects the employee filter %j when recovering shared-shift participants', (userIds, expectedIds) => {
    setActivePinia(createPinia());
    useUsersStore().allUsers = ['selected-user', 'other-user'].map((id) => ({
      id,
      idirName: id,
      firstName: id,
      lastName: 'User',
      email: '',
      employeeNumber: '',
      isEnabled: true,
      gender: 'Male',
      homeLocationId: 15,
      userLocations: [{ locationId: 12, effectiveUntil: '2026-11-13T08:00:00Z' }],
    }));
    const model = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.events',
            resources: [],
            events: [
              {
                id: 'shared-shift',
                type: 'scheduling.shift',
                sourceModule: 'scheduling',
                title: 'Shared shift',
                start: '2026-11-12T17:00:00Z',
                end: '2026-11-13T01:00:00Z',
                resourceIds: ['selected-user', 'other-user'],
                metadata: { userIds: ['selected-user', 'other-user'] },
              } as CalendarSchedulingEvent,
            ],
          },
        },
      },
      { startDate: '2026-11-12', endDate: '2026-11-13', locationId: 12, filters: { userIds } },
      'day',
    );
    expect(model.primaryColumn.resources.map((resource) => resource.id)).toEqual(expect.arrayContaining(expectedIds));
    expect(model.primaryColumn.resources).toHaveLength(expectedIds.length);
  });

  it.each([
    ['America/Vancouver', false],
    ['America/Toronto', true],
  ])('uses the calendar timezone for the exclusive end date (%s)', (timeZone, expected) => {
    const historicalUser = {
      homeLocationId: 15,
      userLocations: [{ locationId: 12, effectiveFrom: null, effectiveUntil: '2026-11-13T07:00:00Z' }],
    };
    expect(isAvailableForSchedule(new Date('2026-11-12T20:00:00Z'), historicalUser, 12, timeZone)).toBe(expected);
  });
  it('disables both borrowed employees outside loan-out dates even when one has no history', () => {
    setActivePinia(createPinia());
    useUsersStore().allUsers = ['first', 'second'].map((id) => ({
      id,
      idirName: id,
      firstName: id,
      lastName: 'User',
      email: '',
      employeeNumber: '',
      isEnabled: true,
      gender: 'Male',
      homeLocationId: 12,
      userLocations: id === 'first' ? [{ locationId: 10, effectiveUntil: '2026-10-01T07:00:00Z' }] : [],
    }));
    const model = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.events',
            events: [],
            resources: [],
          },
          'scheduling.loanout': {
            moduleId: 'loanout',
            contributionId: 'scheduling.loanout',
            events: [],
            resources: ['first', 'second'].map((id) => ({
              id,
              type: 'user',
              sourceModule: 'loanout',
              label: id,
              title: id,
              homeLocationId: 12,
              loanOutMetaData: [{ startAtUtc: '2026-10-28T07:00:00Z', endAtUtc: '2026-10-29T07:00:00Z' }],
            })),
          },
        },
      },
      { startDate: '2026-10-26', endDate: '2026-11-02', locationId: 15, filters: {} },
      'week',
    );
    for (const userId of ['first', 'second']) {
      const cells = model.cells.filter((cell) => cell.resourceId === userId);
      expect(cells).toHaveLength(7);
      expect(cells.filter((cell) => !cell.disabled).map((cell) => cell.date)).toEqual(['2026-10-28']);
    }
  });
  const user = {
    homeLocationId: 15,
    userLocations: [{ locationId: 12, effectiveFrom: null, effectiveUntil: '2026-11-13T08:00:00Z' }],
  };

  it.each([
    ['2026-11-13T07:59:59Z', 12, true],
    ['2026-11-13T08:00:00Z', 12, false],
    ['2026-11-13T07:59:59Z', 15, false],
    ['2026-11-13T08:00:00Z', 15, true],
    ['2026-11-13T08:00:00Z', 99, false],
  ])('checks membership at %s for location %s', (date, locationId, expected) => {
    expect(isAvailableForSchedule(new Date(date), user, locationId)).toBe(expected);
  });

  it('includes the start boundary and excludes the required end boundary', () => {
    const assignment = {
      userLocations: [
        { locationId: 12, effectiveFrom: '2026-11-13T08:00:00Z', effectiveUntil: '2026-11-14T08:00:00Z' },
      ],
    };
    expect(isAvailableForSchedule(new Date('2026-11-13T08:00:00Z'), assignment, 12)).toBe(true);
    expect(isAvailableForSchedule(new Date('2026-11-13T07:59:59Z'), assignment, 12)).toBe(false);
    expect(isAvailableForSchedule(new Date('2026-11-14T08:00:00Z'), assignment, 12)).toBe(false);
  });

  it('uses the latest transfer date even when history is not sorted', () => {
    const movedUser = {
      homeLocationId: 15,
      userLocations: [
        { locationId: 12, effectiveUntil: '2026-11-13T20:00:00Z' },
        { locationId: 10, effectiveUntil: '2026-10-01T07:00:00Z' },
      ],
    };
    expect(isAvailableForSchedule(new Date('2026-11-12T08:00:00Z'), movedUser, 15)).toBe(false);
    expect(isAvailableForSchedule(new Date('2026-11-13T08:00:00Z'), movedUser, 15)).toBe(true);
  });

  it('returns true when location history is missing or empty', () => {
    expect(isAvailableForSchedule(new Date(), { homeLocationId: 12 }, 12)).toBe(true);
    expect(isAvailableForSchedule(new Date(), { homeLocationId: 12 }, 15)).toBe(false);
    expect(isAvailableForSchedule(new Date(), { homeLocationId: 12, userLocations: [] }, 15)).toBe(false);
  });

  it('returns false for missing inputs or an invalid date', () => {
    expect(isAvailableForSchedule(new Date(), undefined, 12)).toBe(false);
    expect(isAvailableForSchedule(new Date(), user, undefined)).toBe(false);
    expect(isAvailableForSchedule(new Date('invalid'), user, 12)).toBe(false);
  });
});
