import { beforeEach, describe, expect, it, vi } from 'vitest';

describe('calendarSchedulingEventsContribution', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.useRealTimers();
  });

  it('keeps one resource row per location-matched user even when some users have no shift events', async () => {
    const postApiSchedulingCalendarEvents = vi.fn().mockImplementation(() => ({
      data: {
        value: {
          moduleId: 'scheduling',
          contributionId: 'scheduling.shift-events',
          events: [
            {
              id: 'shift-1',
              type: 'scheduling.shift',
              sourceModule: 'scheduling',
              title: 'Morning shift',
              start: '2025-04-07T16:00:00Z',
              end: '2025-04-08T00:00:00Z',
              allDay: false,
              isException: false,
              eventTypeCode: 'shift',
              statusTypeCode: 'Draft',
              isConflict: true,
              userIds: ['user-1'],
              resourceIds: ['user-1'],
            },
          ],
        },
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    const getApiUsers = vi.fn().mockImplementation(() => ({
      data: {
        value: [
          {
            id: 'user-1',
            idirName: 'alpha',
            idirId: 'idir-alpha',
            isEnabled: true,
            firstName: 'Alex',
            lastName: 'Alpha',
            email: 'alex.alpha@example.com',
            gender: 'Male',
            rank: 'Cst',
            badgeNumber: '1001',
            homeLocationId: 12,
            lastLogin: null,
          },
          {
            id: 'user-2',
            idirName: 'bravo',
            idirId: 'idir-bravo',
            isEnabled: true,
            firstName: 'Blair',
            lastName: 'Bravo',
            email: 'blair.bravo@example.com',
            gender: 'Female',
            rank: 'Cpl',
            badgeNumber: '1002',
            homeLocationId: 12,
            lastLogin: null,
          },
        ],
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));
    const getApiUsersUserIdActingPositions = vi.fn().mockImplementation(() => ({
      data: { value: [] },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    vi.doMock('@/api-access/generated/scheduling-calendar/scheduling-calendar', () => ({
      postApiSchedulingCalendarEvents,
    }));

    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));

    vi.doMock('@/api-access/generated/acting-positions/acting-positions', () => ({
      getApiUsersUserIdActingPositions,
    }));

    const { calendarSchedulingEventsContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingEventsContribution');

    const result = await calendarSchedulingEventsContribution.load(
      {
        startDate: '2025-04-07',
        endDate: '2025-04-14',
        locationId: 12,
        filters: {},
      },
      { signal: new AbortController().signal },
    );

    expect(getApiUsers).toHaveBeenCalledWith(
      {
        IsEnabled: true,
        LocationId: 12,
      },
      expect.any(Object),
    );

    expect(result.resources).toEqual([
      expect.objectContaining({ id: 'user-1', title: 'Alex Alpha' }),
      expect.objectContaining({ id: 'user-2', title: 'Blair Bravo' }),
    ]);
    expect(result.events[0]).toEqual(expect.objectContaining({ isConflict: true }));
  });

  it('narrows resource rows to explicit user filters when present', async () => {
    const postApiSchedulingCalendarEvents = vi.fn().mockImplementation(() => ({
      data: {
        value: {
          moduleId: 'scheduling',
          contributionId: 'scheduling.shift-events',
          events: [],
        },
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    const getApiUsers = vi.fn().mockImplementation(() => ({
      data: {
        value: [
          {
            id: 'user-1',
            idirName: 'alpha',
            idirId: 'idir-alpha',
            isEnabled: true,
            firstName: 'Alex',
            lastName: 'Alpha',
            email: 'alex.alpha@example.com',
            gender: 'Male',
            rank: null,
            badgeNumber: null,
            homeLocationId: 12,
            lastLogin: null,
          },
          {
            id: 'user-2',
            idirName: 'bravo',
            idirId: 'idir-bravo',
            isEnabled: true,
            firstName: 'Blair',
            lastName: 'Bravo',
            email: 'blair.bravo@example.com',
            gender: 'Female',
            rank: null,
            badgeNumber: null,
            homeLocationId: 12,
            lastLogin: null,
          },
        ],
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));
    const getApiUsersUserIdActingPositions = vi.fn().mockImplementation(() => ({
      data: { value: [] },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    vi.doMock('@/api-access/generated/scheduling-calendar/scheduling-calendar', () => ({
      postApiSchedulingCalendarEvents,
    }));

    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));

    vi.doMock('@/api-access/generated/acting-positions/acting-positions', () => ({
      getApiUsersUserIdActingPositions,
    }));

    const { calendarSchedulingEventsContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingEventsContribution');

    const result = await calendarSchedulingEventsContribution.load({
      startDate: '2025-04-07',
      endDate: '2025-04-14',
      locationId: 12,
      filters: { userIds: ['user-2'] },
    });

    expect(postApiSchedulingCalendarEvents).toHaveBeenCalledWith(
      expect.objectContaining({ userIds: ['user-2'] }),
      expect.any(Object),
    );

    expect(result.resources).toEqual([expect.objectContaining({ id: 'user-2', title: 'Blair Bravo' })]);
  });

  it('reuses users and acting positions when only the date range changes', async () => {
    const postApiSchedulingCalendarEvents = vi.fn().mockImplementation(() => ({
      data: {
        value: {
          moduleId: 'scheduling',
          contributionId: 'scheduling.shift-events',
          events: [],
        },
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    const getApiUsers = vi.fn().mockImplementation(() => ({
      data: {
        value: [
          {
            id: 'user-1',
            idirName: 'alpha',
            idirId: 'idir-alpha',
            isEnabled: true,
            firstName: 'Alex',
            lastName: 'Alpha',
            email: 'alex.alpha@example.com',
            gender: 'Male',
            rank: 'Cst',
            badgeNumber: '1001',
            homeLocationId: 12,
            lastLogin: null,
          },
        ],
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));
    const getApiUsersUserIdActingPositions = vi.fn().mockImplementation(() => ({
      data: { value: [] },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    vi.doMock('@/api-access/generated/scheduling-calendar/scheduling-calendar', () => ({
      postApiSchedulingCalendarEvents,
    }));

    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));

    vi.doMock('@/api-access/generated/acting-positions/acting-positions', () => ({
      getApiUsersUserIdActingPositions,
    }));

    const { calendarSchedulingEventsContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingEventsContribution');

    await calendarSchedulingEventsContribution.load({
      startDate: '2025-04-07',
      endDate: '2025-04-14',
      locationId: 12,
      filters: {},
    });
    await calendarSchedulingEventsContribution.load({
      startDate: '2025-04-14',
      endDate: '2025-04-21',
      locationId: 12,
      filters: {},
    });

    expect(postApiSchedulingCalendarEvents).toHaveBeenCalledTimes(2);
    expect(getApiUsers).toHaveBeenCalledTimes(1);
    expect(getApiUsersUserIdActingPositions).toHaveBeenCalledTimes(1);
  });

  it('displays currently valid active acting positions instead of home location metadata', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-07-02T12:00:00Z'));

    const postApiSchedulingCalendarEvents = vi.fn().mockImplementation(() => ({
      data: {
        value: {
          moduleId: 'scheduling',
          contributionId: 'scheduling.shift-events',
          events: [],
        },
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    const getApiUsers = vi.fn().mockImplementation(() => ({
      data: {
        value: [
          {
            id: 'user-1',
            idirName: 'alpha',
            idirId: 'idir-alpha',
            isEnabled: true,
            firstName: 'Alex',
            lastName: 'Alpha',
            email: 'alex.alpha@example.com',
            gender: 'Male',
            rank: 'Cst',
            badgeNumber: '1001',
            homeLocationId: 12,
            lastLogin: null,
          },
        ],
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    const getApiUsersUserIdActingPositions = vi.fn().mockImplementation(() => ({
      data: {
        value: [
          {
            id: 1,
            userId: 'user-1',
            positionTypeCode: 'SGT',
            positionTypeDescription: 'Sergeant',
            startAtUtc: '2026-07-01T00:00:00Z',
            endAtUtc: null,
            expiryAtUtc: null,
          },
          {
            id: 2,
            userId: 'user-1',
            positionTypeCode: 'CPL',
            positionTypeDescription: 'Corporal',
            startAtUtc: '2026-07-03T00:00:00Z',
            endAtUtc: null,
            expiryAtUtc: null,
          },
          {
            id: 3,
            userId: 'user-1',
            positionTypeCode: 'INSP',
            positionTypeDescription: 'Inspector',
            startAtUtc: '2026-07-01T00:00:00Z',
            endAtUtc: '2026-07-02T00:00:00Z',
            expiryAtUtc: null,
          },
          {
            id: 4,
            userId: 'user-1',
            positionTypeCode: 'SST',
            positionTypeDescription: 'Staff Sergeant',
            startAtUtc: '2026-07-01T00:00:00Z',
            endAtUtc: null,
            expiryAtUtc: '2026-07-02T00:00:00Z',
          },
        ],
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    vi.doMock('@/api-access/generated/scheduling-calendar/scheduling-calendar', () => ({
      postApiSchedulingCalendarEvents,
    }));

    vi.doMock('@/api-access/generated/users/users', () => ({
      getApiUsers,
    }));

    vi.doMock('@/api-access/generated/acting-positions/acting-positions', () => ({
      getApiUsersUserIdActingPositions,
    }));

    const { calendarSchedulingEventsContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingEventsContribution');

    const result = await calendarSchedulingEventsContribution.load({
      startDate: '2026-07-01',
      endDate: '2026-07-08',
      locationId: 12,
      filters: {},
    });

    expect(getApiUsersUserIdActingPositions).toHaveBeenCalledWith('user-1', expect.any(Object));
    expect(result.resources).toEqual([
      expect.objectContaining({
        id: 'user-1',
        meta: [{ value: 'Sergeant' }, { value: '1001' }],
      }),
    ]);
  });
});
