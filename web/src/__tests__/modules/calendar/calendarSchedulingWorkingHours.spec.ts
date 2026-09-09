import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Permissions } from '@/api-access/generated/models';
import {
  createWorkingHoursLookup,
  formatWorkingHoursMinutes,
} from '@/modules/scheduling/calendarSchedulingWorkingHours';
import { calendarSchedulingHeaderShowOvertimeAction } from '@/modules/scheduling/calendarSchedulingActions';
import {
  calendarSchedulingWorkingHoursSelection,
  closeCalendarSchedulingWorkingHours,
} from '@/modules/scheduling/calendarSchedulingState';
import { calendarSchedulingActionIds } from '@/modules/scheduling/calendarSchedulingActionIds';

describe('calendarSchedulingWorkingHoursContribution', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('is available only for the scheduling matrix with required permissions', async () => {
    const { calendarSchedulingWorkingHoursContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingWorkingHoursContribution');
    const runtimeContext = {
      featureFlags: { Scheduling: { enabled: true } },
      permissions: [Permissions.AssignmentsView, Permissions.ShiftsView],
    } as never;
    const queryContext = {
      startDate: '2026-09-07',
      endDate: '2026-09-14',
      filters: {},
    };

    expect(
      calendarSchedulingWorkingHoursContribution.isAvailable?.(runtimeContext, {
        ...queryContext,
        activeViewId: 'calendar.matrix-schedule',
      }),
    ).toBe(true);
    expect(
      calendarSchedulingWorkingHoursContribution.isAvailable?.(runtimeContext, {
        ...queryContext,
        activeViewId: 'calendar.matrix-assignment',
      }),
    ).toBe(false);
    expect(
      calendarSchedulingWorkingHoursContribution.isAvailable?.(
        { ...runtimeContext, featureFlags: { Scheduling: { enabled: false } } },
        { ...queryContext, activeViewId: 'calendar.matrix-schedule' },
      ),
    ).toBe(false);
    expect(
      calendarSchedulingWorkingHoursContribution.isAvailable?.(
        { ...runtimeContext, permissions: [Permissions.AssignmentsView] },
        { ...queryContext, activeViewId: 'calendar.matrix-schedule' },
      ),
    ).toBe(false);
    expect(
      calendarSchedulingWorkingHoursContribution.isAvailable?.(
        { ...runtimeContext, permissions: [Permissions.ShiftsView] },
        { ...queryContext, activeViewId: 'calendar.matrix-schedule' },
      ),
    ).toBe(false);
  });

  it('returns an empty contribution without requesting data when no location is selected', async () => {
    const { calendarSchedulingWorkingHoursContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingWorkingHoursContribution');

    const result = await calendarSchedulingWorkingHoursContribution.load({
      startDate: '2026-09-07',
      endDate: '2026-09-14',
      activeViewId: 'calendar.matrix-schedule',
      filters: {},
    });

    expect(result).toEqual({
      moduleId: 'scheduling',
      contributionId: 'scheduling.working-hours',
      events: [],
      data: { results: [] },
    });
  });

  it('sends calendar date, location, and user filters', async () => {
    const execute = vi.fn().mockResolvedValue(undefined);
    const postApiSchedulingWorkingHoursQuery = vi.fn().mockReturnValue({
      data: { value: [] },
      error: { value: null },
      execute,
    });
    vi.doMock('@/api-access/generated/working-hours/working-hours', () => ({
      postApiSchedulingWorkingHoursQuery,
    }));

    const { calendarSchedulingWorkingHoursContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingWorkingHoursContribution');
    const signal = new AbortController().signal;

    await calendarSchedulingWorkingHoursContribution.load(
      {
        startDate: '2026-09-07',
        endDate: '2026-09-14',
        activeViewId: 'calendar.matrix-schedule',
        locationId: 12,
        filters: { userIds: ['user-1'] },
      },
      { signal },
    );

    expect(postApiSchedulingWorkingHoursQuery).toHaveBeenCalledWith(
      {
        startDate: '2026-09-07',
        endDate: '2026-09-13',
        shiftLocationIds: [12],
        userIds: ['user-1'],
      },
      {
        fetchOptions: { signal },
        options: { immediate: false },
      },
    );
    expect(execute).toHaveBeenCalledOnce();
  });
});

describe('createWorkingHoursLookup', () => {
  it('finds an overnight shift by shift entry and user without a rendered date key', () => {
    const result = {
      shiftEntryId: 42,
      userId: 'user-1',
      date: '2026-09-07',
      paidShiftMinutes: 480,
      lunchAvailableMinutes: 60,
      workedLunchMinutes: 0,
      paidOutsideShiftMinutes: 30,
      creditedMinutes: 510,
      overtimeMinutes: 30,
    };
    const lookup = createWorkingHoursLookup({
      contributions: {
        'scheduling.working-hours': {
          moduleId: 'scheduling',
          contributionId: 'scheduling.working-hours',
          events: [],
          data: { results: [result] },
        },
      },
    });

    expect(lookup.get(42, 'user-1')).toEqual(result);
    expect(lookup.get(42, 'user-2')).toBeUndefined();
  });

  it('ignores missing or malformed contribution data', () => {
    expect(createWorkingHoursLookup({ contributions: {} }).get(42, 'user-1')).toBeUndefined();
    expect(
      createWorkingHoursLookup({
        contributions: {
          'scheduling.working-hours': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.working-hours',
            events: [],
            data: { results: null },
          },
        },
      }).get(42, 'user-1'),
    ).toBeUndefined();
  });
});

describe('formatWorkingHoursMinutes', () => {
  it.each([
    [0, '0m'],
    [30, '30m'],
    [60, '1h'],
    [90, '1h 30m'],
  ])('formats %i minutes as %s', (minutes, expected) => {
    expect(formatWorkingHoursMinutes(minutes)).toBe(expected);
  });
});

describe('calendarSchedulingHeaderShowOvertimeAction', () => {
  it('opens the working-hours result supplied by the shift header action', () => {
    const result = {
      shiftEntryId: 42,
      userId: 'user-1',
      date: '2026-09-07',
      paidShiftMinutes: 480,
      lunchAvailableMinutes: 60,
      workedLunchMinutes: 30,
      paidOutsideShiftMinutes: 0,
      creditedMinutes: 510,
      overtimeMinutes: 30,
    };

    calendarSchedulingHeaderShowOvertimeAction.execute(
      {
        actionId: calendarSchedulingActionIds.showOvertime,
        payload: result,
        cell: { resourceId: 'user-1', date: '2026-09-08', groups: [] },
        header: { text: '11:00 PM - 7:00 AM' },
        model: {
          days: [],
          primaryColumn: { label: 'TEAM', resources: [{ id: 'user-1', type: 'user', title: 'Alex Alpha' }] },
          cells: [],
        },
      },
      { featureFlags: {} },
    );

    expect(calendarSchedulingWorkingHoursSelection.value).toEqual({ result, userName: 'Alex Alpha' });
    closeCalendarSchedulingWorkingHours();
  });

  it('does nothing when no working-hours result is supplied', () => {
    closeCalendarSchedulingWorkingHours();

    calendarSchedulingHeaderShowOvertimeAction.execute(
      {
        actionId: calendarSchedulingActionIds.showOvertime,
        cell: { resourceId: 'user-1', date: '2026-09-08', groups: [] },
        header: { text: '11:00 PM - 7:00 AM' },
        model: {
          days: [],
          primaryColumn: { label: 'TEAM', resources: [] },
          cells: [],
        },
      },
      { featureFlags: {} },
    );

    expect(calendarSchedulingWorkingHoursSelection.value).toBeUndefined();
  });
});
