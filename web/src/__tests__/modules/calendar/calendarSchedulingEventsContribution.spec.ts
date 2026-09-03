import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

describe('calendarSchedulingEventsContribution', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.useRealTimers();
    setActivePinia(createPinia());
  });

  it('fails closed and does not fetch scheduling data without an active location', async () => {
    const postApiSchedulingCalendarEvents = vi.fn();
    const getApiUsers = vi.fn();
    const getApiUsersUserIdActingPositions = vi.fn();

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
      filters: {},
    });

    expect(result.events).toEqual([]);
    expect(result.resources).toEqual([]);
    expect(postApiSchedulingCalendarEvents).not.toHaveBeenCalled();
    expect(getApiUsers).not.toHaveBeenCalled();
    expect(getApiUsersUserIdActingPositions).not.toHaveBeenCalled();
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
    expect(result.events[0]).not.toHaveProperty('isConflict');
  });

  it('maps assignment links and users into scheduling event metadata', async () => {
    const userId = '868d8b04-13ff-4b25-bd36-87c90a0d032d';
    const postApiSchedulingCalendarEvents = vi.fn().mockReturnValue({
      data: {
        value: {
          moduleId: 'scheduling',
          contributionId: 'scheduling.events',
          events: [
            {
              id: 'scheduling.assignment-entry.201',
              shiftEntryId: null,
              shiftSeriesId: null,
              assignmentEntryId: 201,
              assignmentSeriesId: null,
              eventId: 211,
              userIds: [userId],
              type: 'scheduling.assignment',
              sourceModule: 'scheduling',
              title: 'Court Room Monitor',
              color: 'pink',
              start: '2026-08-25T16:00:00+00:00',
              end: '2026-08-26T00:00:00+00:00',
              eventTypeCode: 'assignment',
              statusTypeCode: 'draft',
              locationId: 1,
              resourceIds: [userId],
              categoryId: 6,
              categoryName: 'Documents Criminal',
              subCategoryId: 25,
              subCategoryName: 'Courts Orders',
              capacity: 1,
              assignedUserCount: 1,
              linkedShiftEntryIds: [206],
              assignedUserIds: [userId],
            },
            {
              id: 'scheduling.shift-entry.206',
              shiftEntryId: 206,
              shiftSeriesId: 201,
              assignmentEntryId: null,
              assignmentSeriesId: null,
              eventId: 206,
              userIds: [userId],
              type: 'scheduling.shift',
              sourceModule: 'scheduling',
              title: 'Mary Park shift',
              start: '2026-08-25T16:00:00+00:00',
              end: '2026-08-26T00:00:00+00:00',
              eventTypeCode: 'shift',
              statusTypeCode: 'draft',
              locationId: 1,
              resourceIds: [userId],
            },
          ],
        },
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    });
    const getApiUsers = vi.fn().mockReturnValue({
      data: {
        value: [
          {
            id: userId,
            idirName: 'mpark',
            firstName: 'Mary',
            lastName: 'Park',
          },
        ],
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    });
    const getApiUsersUserIdActingPositions = vi.fn().mockReturnValue({
      data: { value: [] },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    });

    vi.doMock('@/api-access/generated/scheduling-calendar/scheduling-calendar', () => ({
      postApiSchedulingCalendarEvents,
    }));
    vi.doMock('@/api-access/generated/users/users', () => ({ getApiUsers }));
    vi.doMock('@/api-access/generated/acting-positions/acting-positions', () => ({
      getApiUsersUserIdActingPositions,
    }));

    const { calendarSchedulingEventsContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingEventsContribution');

    const result = await calendarSchedulingEventsContribution.load({
      startDate: '2026-08-24',
      endDate: '2026-08-31',
      locationId: 1,
      filters: { timeZoneId: 'America/Vancouver' },
    });

    expect(result.events[0]).toMatchObject({
      metadata: {
        assignmentEntryId: '201',
        assignedShiftIds: ['206'],
        assignedUserIds: [userId],
        capacity: 1,
        assignedCount: 1,
        categoryId: 6,
        subCategoryId: 25,
      },
    });

    const { buildCalendarSchedulingViewModel } = await import('@/modules/scheduling/calendarSchedulingMappers');
    const viewModel = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'scheduling.events': result,
        },
      },
      {
        startDate: '2026-08-24',
        endDate: '2026-08-31',
        locationId: 1,
        filters: { timeZoneId: 'America/Vancouver' },
      },
      'week',
    );

    expect(viewModel.primaryColumn.resources.map((resource) => resource.id)).toEqual([userId]);
    expect(
      viewModel.cells
        .find((cell) => cell.resourceId === userId && cell.date === '2026-08-25')
        ?.groups[0]?.events.map((item) => item.event.id),
    ).toEqual(['scheduling.assignment-entry.201']);
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
    expect(getApiUsers).toHaveBeenCalledTimes(2);
    expect(getApiUsersUserIdActingPositions).toHaveBeenCalledTimes(1);
  });

  it('maps assigned users from all enabled users while keeping resource rows location-filtered', async () => {
    const postApiSchedulingCalendarEvents = vi.fn().mockImplementation(() => ({
      data: {
        value: {
          moduleId: 'scheduling',
          contributionId: 'scheduling.shift-events',
          events: [
            {
              id: 'assignment-1',
              type: 'scheduling.assignment',
              sourceModule: 'scheduling',
              title: 'Blue Assignment',
              start: '2026-07-13T16:00:00Z',
              end: '2026-07-14T00:00:00Z',
              allDay: false,
              isException: false,
              eventTypeCode: 'assignment',
              statusTypeCode: 'Draft',
              assignedUserIds: ['external-user'],
              resourceIds: [],
            },
          ],
        },
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }));

    const getApiUsers = vi.fn().mockImplementation((request: { LocationId?: number }) => ({
      data: {
        value:
          request.LocationId === 12
            ? [
                {
                  id: 'local-user',
                  idirName: 'local',
                  idirId: 'idir-local',
                  isEnabled: true,
                  firstName: 'Local',
                  lastName: 'User',
                  email: 'local.user@example.com',
                  gender: 'Unknown',
                  rank: 'Sheriff',
                  badgeNumber: '1001',
                  homeLocationId: 12,
                  lastLogin: null,
                },
              ]
            : [
                {
                  id: 'local-user',
                  idirName: 'local',
                  idirId: 'idir-local',
                  isEnabled: true,
                  firstName: 'Local',
                  lastName: 'User',
                  email: 'local.user@example.com',
                  gender: 'Unknown',
                  rank: 'Sheriff',
                  badgeNumber: '1001',
                  homeLocationId: 12,
                  lastLogin: null,
                },
                {
                  id: 'external-user',
                  idirName: 'external',
                  idirId: 'idir-external',
                  isEnabled: true,
                  firstName: 'Chief',
                  lastName: 'Sheriff',
                  email: 'chief.sheriff@example.com',
                  gender: 'Unknown',
                  rank: 'Chief Sheriff',
                  badgeNumber: '9001',
                  homeLocationId: 99,
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
      startDate: '2026-07-13',
      endDate: '2026-07-14',
      locationId: 12,
      filters: {},
    });

    expect(result.resources).toEqual([expect.objectContaining({ id: 'local-user' })]);
    expect(result.events[0]).toEqual(
      expect.objectContaining({
        metadata: expect.objectContaining({
          assignedUsers: [expect.objectContaining({ id: 'external-user', title: 'Chief Sheriff' })],
        }),
      }),
    );
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
