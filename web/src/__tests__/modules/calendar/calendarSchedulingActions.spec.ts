import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { CalendarMatrixDropActionContext } from '@/modules/calendar/registry/calendarActionRegistryTypes';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import { calendarActionRegistry } from '@/modules/calendar/registry/calendarActionRegistry';
import { useCalendarAlertStore } from '@/modules/calendar/calendarAlertStore';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { Permissions } from '@/api-access/generated/models';
import { calendarSchedulingActionIds } from '@/modules/scheduling/calendarSchedulingActionIds';
import { calendarAssignmentViewContribution } from '@/modules/scheduling/calendarAssignmentViewContribution';
import { calendarShiftViewContribution } from '@/modules/scheduling/calendarShiftViewContribution';
import { registerCalendarSchedulingModule } from '@/modules/scheduling/CalendarSchedulingModule';
import {
  calendarAddAssignmentAction,
  calendarAddResourceAction,
  calendarAddAssignmentResourceAction,
  calendarDropAction,
  calendarDropUserOnAssignmentResourceAction,
  calendarScheduleStaffAction,
  calendarSchedulingCreateShiftAction,
  calendarSchedulingEventDetailAction,
  calendarSchedulingHeaderShowConflictAction,
  calendarSchedulingPublishAction,
  calendarSchedulingResolveConflictAction,
  calendarSchedulingShowConflictAction,
} from '@/modules/scheduling/calendarSchedulingActions';
import {
  calendarSchedulingAssignmentModalAssignmentDefinitionId,
  calendarSchedulingAssignmentModalAssignedUserId,
  calendarSchedulingAssignmentModalDraggedUserOnly,
  calendarSchedulingAssignmentModalDate,
  calendarSchedulingAssignmentModalEntryId,
  calendarSchedulingAssignmentModalMode,
  calendarSchedulingAssignmentModalSeriesId,
  calendarSchedulingAssignmentModalShiftEntryIds,
  calendarSchedulingConflictEventId,
  calendarSchedulingConflictHeaderId,
  calendarSchedulingDetailEvent,
  calendarSchedulingDetailInitialOpenScope,
  calendarSchedulingDetailInitialTab,
  calendarSchedulingResourceActionDate,
  calendarSchedulingResourceActionAssignmentEntryId,
  calendarSchedulingResourceActionAssignmentEvents,
  calendarSchedulingResourceActionResource,
  calendarSchedulingPublishRequest,
  closeCalendarSchedulingAssignmentModal,
  closeCalendarSchedulingEventDetail,
  closeCalendarSchedulingPublishModal,
  closeCalendarSchedulingResourceActionModal,
  getCalendarSchedulingHeaderConflictKey,
  showCalendarSchedulingEventDetail,
} from '@/modules/scheduling/calendarSchedulingState';

describe('calendarSchedulingActions', () => {
  const writableRuntimeContext = {
    featureFlags: {},
    permissions: Object.values(Permissions),
  };
  beforeEach(() => {
    setActivePinia(createPinia());
    closeCalendarSchedulingAssignmentModal();
    closeCalendarSchedulingEventDetail();
    closeCalendarSchedulingPublishModal();
    closeCalendarSchedulingResourceActionModal();
  });

  it('makes create shift available only in the schedule view for shift or leave creators', () => {
    const createContext = {
      startDate: '2026-07-14',
      endDate: '2026-07-14',
      activeViewId: 'calendar-default',
      filters: {},
    };
    const scheduleContext = { ...createContext, activeViewId: 'calendar.matrix-schedule' };
    const shiftCreator = {
      featureFlags: {},
      permissions: [Permissions.ShiftsCreateAndAssign],
    };
    const leaveCreator = {
      featureFlags: { Leave: { enabled: true } },
      permissions: [Permissions.LeaveCreateAndAssign],
    };
    const noPermissions = {
      featureFlags: { Leave: { enabled: true } },
      permissions: [],
    };

    expect(calendarSchedulingCreateShiftAction.isAvailable?.(scheduleContext, shiftCreator)).toBe(true);
    expect(calendarSchedulingCreateShiftAction.isAvailable?.(createContext, shiftCreator)).toBe(false);
    expect(calendarSchedulingCreateShiftAction.isAvailable?.(scheduleContext, leaveCreator)).toBe(true);
    expect(calendarSchedulingCreateShiftAction.isAvailable?.(createContext, leaveCreator)).toBe(false);
    expect(calendarSchedulingCreateShiftAction.isAvailable?.(scheduleContext, noPermissions)).toBe(false);
  });

  it('shows publish only for an authorized location scope', () => {
    const context = {
      startDate: '2026-08-24',
      endDate: '2026-08-30',
      locationId: 7,
      filters: {},
    };

    expect(calendarSchedulingPublishAction.isAvailable?.(context, { featureFlags: {}, permissions: [] })).toBe(false);
    expect(
      calendarSchedulingPublishAction.isAvailable?.(context, {
        featureFlags: {},
        permissions: [Permissions.SchedulePublish],
      }),
    ).toBe(true);
    expect(
      calendarSchedulingPublishAction.isAvailable?.(
        { ...context, locationId: undefined },
        { featureFlags: {}, permissions: [Permissions.SchedulePublish] },
      ),
    ).toBe(false);
  });

  it('opens publish for the displayed location and date range', async () => {
    await calendarSchedulingPublishAction.run(
      {
        startDate: '2026-08-24',
        endDate: '2026-08-30',
        locationId: 7,
        filters: {},
      },
      { featureFlags: {}, permissions: [Permissions.SchedulePublish] },
    );

    expect(calendarSchedulingPublishRequest.value).toEqual({
      locationId: 7,
      startDate: '2026-08-24',
      endDate: '2026-08-29',
    });
  });

  it.each([
    ['day', '2026-10-22', '2026-10-23', '2026-10-22'],
    ['week', '2026-10-19', '2026-10-26', '2026-10-25'],
    ['work week', '2026-10-19', '2026-10-24', '2026-10-23'],
    ['month', '2026-10-01', '2026-11-01', '2026-10-31'],
    ['DST boundary', '2026-03-08', '2026-03-09', '2026-03-08'],
  ])(
    'publishes the inclusive %s range in Schedule and Assignment views',
    async (_period, startDate, endDate, publishEndDate) => {
      registerCalendarSchedulingModule();

      for (const viewId of [calendarShiftViewContribution.id, calendarAssignmentViewContribution.id]) {
        const action = calendarActionRegistry
          .getToolbarActionsForView(viewId)
          .find((candidate) => candidate.id === calendarSchedulingPublishAction.id);
        expect(action).toBe(calendarSchedulingPublishAction);

        await action!.run(
          { startDate, endDate, locationId: 7, filters: {} },
          { featureFlags: {}, permissions: [Permissions.SchedulePublish] },
        );

        expect(calendarSchedulingPublishRequest.value).toEqual({
          locationId: 7,
          startDate,
          endDate: publishEndDate,
        });
        closeCalendarSchedulingPublishModal();
      }
    },
  );

  it('does not open publish without permission or a location', async () => {
    const context = { startDate: '2026-10-19', endDate: '2026-10-26', locationId: 7, filters: {} };

    await calendarSchedulingPublishAction.run(context, { featureFlags: {}, permissions: [] });
    expect(calendarSchedulingPublishRequest.value).toBeUndefined();

    await calendarSchedulingPublishAction.run(
      { ...context, locationId: undefined },
      { featureFlags: {}, permissions: [Permissions.SchedulePublish] },
    );
    expect(calendarSchedulingPublishRequest.value).toBeUndefined();
  });

  it('opens the add assignment modal on the first date in the displayed range', async () => {
    await calendarAddAssignmentAction.execute(
      {
        panel: {
          label: 'ASSIGNMENTS',
          actionId: 'calendar-scheduling.add-assignment',
          actionLabel: 'Add Assignment',
          items: [],
        },
        actionId: 'calendar-scheduling.add-assignment',
        model: {
          timeZone: 'America/Vancouver',
          days: [
            { date: '2026-07-27', label: 'Mon, Jul 27' },
            { date: '2026-07-31', label: 'Fri, Jul 31', isToday: true },
          ],
          primaryColumn: {
            label: 'TEAM',
            resources: [],
          },
          cells: [],
        },
      },
      writableRuntimeContext,
    );

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-27');
  });

  it('shows and resolves conflicts for canonical scheduling assignment events', async () => {
    const event = {
      id: 'assignment-entry-90',
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Court Room Monitor',
      start: '2026-07-12T16:00:00Z',
      metadata: { assignmentEntryId: '90' },
    };
    const model = {
      timeZone: 'America/Vancouver',
      days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
      primaryColumn: { label: 'TEAM', resources: [] },
      cells: [],
    };
    const showContext = {
      event,
      actionId: calendarSchedulingActionIds.showConflict,
      actionType: 'button' as const,
      model,
    };
    calendarSchedulingConflictHeaderId.value = event.id;

    expect(calendarSchedulingShowConflictAction.isAvailable?.(showContext, writableRuntimeContext)).toBe(true);
    await calendarSchedulingShowConflictAction.execute(showContext, writableRuntimeContext);
    expect(calendarSchedulingConflictEventId.value).toBe(event.id);
    expect(calendarSchedulingConflictHeaderId.value).toBeUndefined();

    const resolveContext = {
      ...showContext,
      actionId: calendarSchedulingActionIds.resolveConflict,
    };
    expect(calendarSchedulingResolveConflictAction.isAvailable?.(resolveContext, writableRuntimeContext)).toBe(true);
    await calendarSchedulingResolveConflictAction.execute(resolveContext, writableRuntimeContext);
    expect(calendarSchedulingConflictEventId.value).toBeUndefined();
  });

  it('opens a shift-header conflict only for the clicked matrix cell', async () => {
    const cell = {
      resourceId: 'user-1',
      date: '2026-07-12',
      groups: [],
    };
    const header = {
      id: 'shift-entry-44',
      text: '9:00 AM - 5:00 PM',
      payload: {
        id: 'shift-entry-44',
        type: 'scheduling.shift',
        sourceModule: 'scheduling',
        title: 'Day shift',
        start: '2026-07-12T16:00:00Z',
      },
    };
    const context = {
      cell,
      header,
      actionId: calendarSchedulingActionIds.showConflict,
      actionType: 'button' as const,
      model: {
        timeZone: 'America/Vancouver',
        days: [],
        primaryColumn: { label: 'TEAM', resources: [] },
        cells: [],
      },
    };

    await calendarSchedulingHeaderShowConflictAction.execute(context, writableRuntimeContext);

    expect(calendarSchedulingConflictHeaderId.value).toBe(getCalendarSchedulingHeaderConflictKey(cell, header.id));
  });

  it('opens the assignment modal for the dropped assignment and pre-links shift entries from the target cell header', async () => {
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: {
          assignmentDefinitionId: 20,
        },
      },
      drop: {
        resourceId: 'user-1',
        resourceType: 'user',
        date: '2026-07-12',
      },
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [{ id: 'user-1', type: 'user', title: 'Target User' }],
        },
        cells: [
          {
            resourceId: 'user-1',
            date: '2026-07-12',
            headers: [
              {
                id: 'shift-44',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-44',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Shift',
                  start: '2026-07-12T16:00:00Z',
                  end: '2026-07-13T00:00:00Z',
                  resourceIds: ['user-1'],
                  metadata: {
                    shiftEntryId: '44',
                    userIds: ['user-1'],
                  },
                },
              },
              {
                id: 'shift-45',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-45',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Other User Shift',
                  start: '2026-07-12T16:00:00Z',
                  end: '2026-07-13T00:00:00Z',
                  resourceIds: ['user-2'],
                  metadata: {
                    shiftEntryId: '45',
                    userIds: ['user-2'],
                  },
                },
              },
            ],
            groups: [],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, {
      ...writableRuntimeContext,
    });

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-12');
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBe(20);
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('shows an error instead of opening the modal when a new assignment is dropped on a user without a shift', async () => {
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: {
          assignmentDefinitionId: 20,
        },
      },
      drop: {
        resourceId: 'user-1',
        resourceType: 'user',
        date: '2026-07-12',
      },
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [{ id: 'user-1', type: 'user', title: 'Target User' }],
        },
        cells: [
          {
            resourceId: 'user-1',
            date: '2026-07-12',
            headers: [],
            groups: [],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, writableRuntimeContext);

    expect(useCalendarAlertStore().alerts).toContainEqual({
      id: 'scheduling.assignment.shift-required',
      message: 'Create a shift for this user on this date before scheduling an assignment.',
      severity: 'error',
      source: 'calendar-scheduling',
    });
    expect(calendarSchedulingAssignmentModalDate.value).toBeUndefined();
  });

  it('edits the existing assignment from the canonical scheduling event payload when it is not rendered in a cell', async () => {
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: {
          title: 'Court Room Monitor',
          assignmentDefinitionId: 20,
        },
      },
      drop: {
        resourceId: 'user-1',
        resourceType: 'user',
        date: '2026-07-12',
      },
      model: {
        timeZone: 'America/Vancouver',
        payload: {
          assignmentEvents: [
            {
              id: 'scheduling.assignment-entry.90',
              type: 'scheduling.assignment',
              sourceModule: 'scheduling',
              title: 'Court Room Monitor',
              start: '2026-07-12T16:00:00Z',
              metadata: { assignmentDefinitionId: '20', assignmentEntryId: '90' },
            },
          ],
        },
        days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [{ id: 'user-1', type: 'user', title: 'Target User' }],
        },
        cells: [
          {
            resourceId: 'user-1',
            date: '2026-07-12',
            headers: [
              {
                id: 'shift-44',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-44',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Shift',
                  start: '2026-07-12T16:00:00Z',
                  resourceIds: ['user-1'],
                  metadata: { shiftEntryId: '44', userIds: ['user-1'] },
                },
              },
            ],
            groups: [],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, writableRuntimeContext);

    expect(calendarSchedulingAssignmentModalMode.value).toBe('edit');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(90);
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBeUndefined();
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('shows an error when an existing assignment belongs to another user and the drop target has no shift', async () => {
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: {
          title: 'Court Room Monitor',
          assignmentDefinitionId: 20,
        },
      },
      drop: {
        resourceId: 'user-1',
        resourceType: 'user',
        date: '2026-07-12',
      },
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [
            { id: 'user-1', type: 'user', title: 'Target User' },
            { id: 'user-2', type: 'user', title: 'Other User' },
          ],
        },
        cells: [
          {
            resourceId: 'user-1',
            date: '2026-07-12',
            headers: [],
            groups: [],
          },
          {
            resourceId: 'user-2',
            date: '2026-07-12',
            headers: [],
            groups: [
              {
                id: 'assignments',
                events: [
                  {
                    event: {
                      id: 'assignment-entry-90',
                      type: 'scheduling.assignment',
                      sourceModule: 'calendar-assignment',
                      title: 'Court Room Monitor',
                      start: '2026-07-12T16:00:00Z',
                      metadata: {
                        assignmentDefinitionId: '20',
                        assignmentEntryId: '90',
                        assignedUserIds: ['user-2'],
                      },
                    } as never,
                  },
                ],
              },
            ],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, writableRuntimeContext);

    expect(useCalendarAlertStore().alerts).toContainEqual({
      id: 'scheduling.assignment.shift-required',
      message: 'Create a shift for this user on this date before scheduling an assignment.',
      severity: 'error',
      source: 'calendar-scheduling',
    });
    expect(calendarSchedulingAssignmentModalDate.value).toBeUndefined();
    expect(calendarSchedulingAssignmentModalEntryId.value).toBeUndefined();
  });

  it('uses the global assignment context when the existing assignment is linked to another user and shift', async () => {
    const existingAssignment = {
      id: 'assignment-entry-90',
      type: 'scheduling.assignment',
      sourceModule: 'calendar-assignment',
      title: 'Court Room Monitor',
      start: '2026-07-12T16:00:00Z',
      metadata: {
        assignmentDefinitionId: '20',
        assignmentEntryId: '90',
        assignedUserIds: ['user-2'],
        assignedShiftIds: ['45'],
      },
    } as never;
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: { assignmentDefinitionId: 20, title: 'Court Room Monitor' },
      },
      drop: {
        resourceId: 'user-1',
        resourceType: 'user',
        date: '2026-07-12',
      },
      model: {
        timeZone: 'America/Vancouver',
        payload: { assignmentEvents: [existingAssignment] },
        days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [
            { id: 'user-1', type: 'user', title: 'Target User' },
            { id: 'user-2', type: 'user', title: 'Other User' },
          ],
        },
        cells: [
          {
            resourceId: 'user-1',
            date: '2026-07-12',
            headers: [
              {
                id: 'shift-44',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-44',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Target shift',
                  start: '2026-07-12T16:00:00Z',
                  resourceIds: ['user-1'],
                  metadata: { shiftEntryId: '44', userIds: ['user-1'] },
                },
              },
            ],
            groups: [],
          },
          {
            resourceId: 'user-2',
            date: '2026-07-12',
            headers: [
              {
                id: 'shift-45',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-45',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Existing assignment shift',
                  start: '2026-07-12T16:00:00Z',
                  resourceIds: ['user-2'],
                  metadata: { shiftEntryId: '45', userIds: ['user-2'] },
                },
              },
            ],
            groups: [],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, writableRuntimeContext);

    expect(calendarSchedulingAssignmentModalMode.value).toBe('edit');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(90);
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
    expect(calendarSchedulingAssignmentModalAssignedUserId.value).toBe('user-1');
  });

  it('pre-links only shifts belonging to the target user row when the drop payload omits resource type', async () => {
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: {
          assignmentDefinitionId: 20,
        },
      },
      drop: {
        resourceId: 'user-1',
        date: '2026-07-12',
      },
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [{ id: 'user-1', type: 'user', title: 'Target User' }],
        },
        cells: [
          {
            resourceId: 'user-1',
            date: '2026-07-12',
            headers: [
              {
                id: 'shift-44',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-44',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Target User Shift',
                  start: '2026-07-12T16:00:00Z',
                  end: '2026-07-13T00:00:00Z',
                  metadata: {
                    shiftEntryId: '44',
                    userIds: ['user-1'],
                  },
                },
              },
              {
                id: 'shift-45',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-45',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Other User Shift',
                  start: '2026-07-12T16:00:00Z',
                  end: '2026-07-13T00:00:00Z',
                  metadata: {
                    shiftEntryId: '45',
                    userIds: ['user-2'],
                  },
                },
              },
            ],
            groups: [],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, {
      ...writableRuntimeContext,
    });

    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('does not pre-link shifts from other locations when dropping an assignment on the schedule view', async () => {
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: {
          assignmentDefinitionId: 20,
          locationId: 12,
        },
      },
      drop: {
        resourceId: 'user-1',
        resourceType: 'user',
        date: '2026-07-12',
      },
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-07-12', label: 'Sun, Jul 12' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [{ id: 'user-1', type: 'user', title: 'Target User' }],
        },
        cells: [
          {
            resourceId: 'user-1',
            date: '2026-07-12',
            headers: [
              {
                id: 'shift-44',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-44',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Same Location Shift',
                  start: '2026-07-12T16:00:00Z',
                  end: '2026-07-13T00:00:00Z',
                  locationId: 12,
                  resourceIds: ['user-1'],
                  metadata: {
                    shiftEntryId: '44',
                    userIds: ['user-1'],
                  },
                },
              },
              {
                id: 'shift-45',
                text: '9:00 AM - 5:00 PM',
                payload: {
                  id: 'shift-45',
                  type: 'scheduling.shift',
                  sourceModule: 'calendar-scheduling',
                  title: 'Other Location Shift',
                  start: '2026-07-12T16:00:00Z',
                  end: '2026-07-13T00:00:00Z',
                  locationId: 13,
                  resourceIds: ['user-1'],
                  metadata: {
                    shiftEntryId: '45',
                    userIds: ['user-1'],
                  },
                },
              },
            ],
            groups: [],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, {
      ...writableRuntimeContext,
    });

    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('opens the assignment view modal for scheduling assignment event details', () => {
    const calendarStore = useCalendarStore();
    calendarStore.setSelectedEvent('scheduling.assignment-entry.200');
    showCalendarSchedulingEventDetail({
      id: 'scheduling.shift-entry.99',
      type: 'scheduling.shift',
      sourceModule: 'scheduling',
      title: 'Existing shift detail',
      start: '2026-07-14T08:00:00+00:00',
    });

    calendarSchedulingEventDetailAction.run({
      event: {
        id: 'scheduling.assignment-entry.200',
        type: 'scheduling.assignment',
        sourceModule: 'scheduling',
        title: 'BLUE ASSIGNMENT',
        start: '2026-07-14T16:00:00+00:00',
        end: '2026-07-16T00:00:00+00:00',
        eventTypeCode: 'assignment',
        metadata: {
          assignmentEntryId: '200',
          assignmentSeriesId: '201',
        },
      },
      runtimeContext: writableRuntimeContext,
    } as never);

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-14');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(200);
    expect(calendarSchedulingAssignmentModalSeriesId.value).toBe(201);
    expect(calendarSchedulingAssignmentModalMode.value).toBe('view');
    expect(calendarSchedulingDetailEvent.value).toBeUndefined();
    expect(calendarStore.selectedEventId).toBeUndefined();
  });

  it('opens the assignment modal with the row assignment definition for assignment resource actions', async () => {
    await calendarAddAssignmentResourceAction.execute(
      {
        resource: {
          id: 'assignment-definition-20',
          type: 'assignment',
          title: 'Courtroom',
          assignmentDefinitionId: 20,
        } as never,
        cell: {
          resourceId: 'assignment-definition-20',
          date: '2026-07-13',
          groups: [],
        },
        actionId: 'calendar-scheduling.add-assignment-resource',
        model: {
          timeZone: 'America/Vancouver',
          days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
          primaryColumn: {
            label: 'ASSIGNMENTS',
            resources: [],
          },
          cells: [],
        },
      },
      writableRuntimeContext,
    );

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-13');
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBe(20);
  });

  it('opens the shift modal with clicked user row, clicked date, and row context', async () => {
    await calendarAddResourceAction.execute(
      {
        resource: {
          id: 'user-1',
          type: 'user',
          title: 'Alex Alpha',
        } as never,
        cell: {
          resourceId: 'user-1',
          date: '2026-07-14',
          groups: [],
        },
        actionId: 'calendar-scheduling.add-resource',
        model: {
          timeZone: 'America/Vancouver',
          days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
          primaryColumn: {
            label: 'TEAM',
            resources: [],
          },
          cells: [],
        },
      },
      writableRuntimeContext,
    );

    expect(calendarSchedulingResourceActionDate.value).toBe('2026-07-14');
    expect(calendarSchedulingResourceActionResource.value).toEqual({
      id: 'user-1',
      type: 'user',
      title: 'Alex Alpha',
    });
  });

  it('makes the add-resource action available when the user can create leave without shifts', () => {
    const context = {
      resource: { id: 'user-1', type: 'user', title: 'Alex Alpha' },
      cell: { resourceId: 'user-1', date: '2026-07-14', groups: [] },
      actionId: calendarSchedulingActionIds.addResource,
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-07-14', label: 'Tue, Jul 14' }],
        primaryColumn: { label: 'TEAM', resources: [] },
        cells: [],
      },
    };
    const leaveOnlyContext = {
      featureFlags: { Leave: { enabled: true } },
      permissions: [Permissions.LeaveCreateAndAssign],
    };

    expect(calendarAddResourceAction.isAvailable?.(context, leaveOnlyContext)).toBe(true);
    expect(calendarAddResourceAction.isAvailable?.(context, { featureFlags: {}, permissions: [] })).toBe(false);
  });

  it('opens the add-resource modal when the user can create leave without shifts', async () => {
    const leaveOnlyContext = {
      featureFlags: { Leave: { enabled: true } },
      permissions: [Permissions.LeaveCreateAndAssign],
    };

    await calendarAddResourceAction.execute(
      {
        resource: { id: 'user-1', type: 'user', title: 'Alex Alpha' } as never,
        cell: { resourceId: 'user-1', date: '2026-07-14', groups: [] },
        actionId: calendarSchedulingActionIds.addResource,
        model: {
          timeZone: 'America/Vancouver',
          days: [{ date: '2026-07-14', label: 'Tue, Jul 14' }],
          primaryColumn: { label: 'TEAM', resources: [] },
          cells: [],
        },
      },
      leaveOnlyContext,
    );

    expect(calendarSchedulingResourceActionDate.value).toBe('2026-07-14');
    expect(calendarSchedulingResourceActionResource.value).toEqual({
      id: 'user-1',
      type: 'user',
      title: 'Alex Alpha',
    });
  });

  it('opens the shift modal with the dragged user and target cell assignments when a user is dropped on an assignment row', async () => {
    const assignmentEvent = {
      id: 'scheduling.assignment-entry.257',
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Yellow Assignment',
      start: '2026-07-13T16:00:00Z',
      end: '2026-07-14T00:00:00Z',
      metadata: {
        assignmentEntryId: '257',
        assignmentDefinitionId: 20,
      },
    };

    await calendarDropUserOnAssignmentResourceAction.execute(
      {
        drag: {
          source: 'side-panel',
          itemId: 'user-1',
          itemType: 'user',
          payload: {
            userId: 'user-1',
            title: 'Alex Alpha',
          },
        },
        drop: {
          resourceId: 'assignment-definition-20',
          resourceType: 'assignment',
          date: '2026-07-13',
        },
        model: {
          timeZone: 'America/Vancouver',
          days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
          primaryColumn: {
            label: 'ASSIGNMENTS',
            resources: [],
          },
          cells: [
            {
              resourceId: 'assignment-definition-20',
              date: '2026-07-13',
              groups: [
                {
                  id: 'assignments',
                  events: [
                    {
                      event: assignmentEvent,
                    },
                  ],
                },
              ],
            },
          ],
        },
      },
      writableRuntimeContext,
    );

    expect(calendarSchedulingResourceActionDate.value).toBe('2026-07-13');
    expect(calendarSchedulingResourceActionResource.value).toEqual({
      id: 'user-1',
      type: 'user',
      title: 'Alex Alpha',
    });
    expect(calendarSchedulingResourceActionAssignmentEntryId.value).toBe(257);
    expect(calendarSchedulingResourceActionAssignmentEvents.value).toEqual([assignmentEvent]);
  });

  it('creates a shift for a user dropped on their own Schedule View date without an existing shift', () => {
    const context = userDropContext('user');
    expect(
      calendarDropUserOnAssignmentResourceAction.isAvailable?.(context, {
        featureFlags: {},
        permissions: [Permissions.ShiftsCreateAndAssign],
      }),
    ).toBe(true);

    calendarDropUserOnAssignmentResourceAction.execute(context, {
      featureFlags: {},
      permissions: [Permissions.ShiftsCreateAndAssign],
    });

    expect(calendarSchedulingResourceActionDate.value).toBe('2026-07-13');
    expect(calendarSchedulingResourceActionResource.value?.id).toBe('user-1');
  });

  it('opens the shared shift for direct occurrence editing on the Schedule View', () => {
    const shift = shiftEvent();
    const context = userDropContext('user', [shift]);
    calendarDropUserOnAssignmentResourceAction.execute(context, {
      featureFlags: {},
      permissions: [Permissions.ShiftsEdit],
    });

    expect(calendarSchedulingDetailEvent.value).toEqual(shift);
    expect(calendarSchedulingDetailInitialOpenScope.value).toBe('event');
    expect(calendarSchedulingDetailInitialTab.value).toBe('edit');
    expect(calendarSchedulingResourceActionDate.value).toBeUndefined();
  });

  it('creates an assignment with only the dragged user selected from a shared shift', () => {
    const context = userDropContext('assignment', [shiftEvent()]);
    calendarDropUserOnAssignmentResourceAction.execute(context, {
      featureFlags: {},
      permissions: [Permissions.AssignmentsCreate, Permissions.AssignmentsAssign],
    });

    expect(calendarSchedulingAssignmentModalMode.value).toBe('create');
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBe(20);
    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-13');
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
    expect(calendarSchedulingAssignmentModalAssignedUserId.value).toBe('user-1');
    expect(calendarSchedulingAssignmentModalDraggedUserOnly.value).toBe(true);
  });

  it('edits only the matching assignment occurrence when the user already has a shift', () => {
    const context = userDropContext(
      'assignment',
      [shiftEvent()],
      [assignmentEvent(257, 20, 211), assignmentEvent(258, 21)],
    );
    calendarDropUserOnAssignmentResourceAction.execute(context, {
      featureFlags: {},
      permissions: [Permissions.AssignmentsEdit, Permissions.AssignmentsAssign],
    });

    expect(calendarSchedulingAssignmentModalMode.value).toBe('edit');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(257);
    expect(calendarSchedulingAssignmentModalSeriesId.value).toBe(211);
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
    expect(calendarSchedulingAssignmentModalDraggedUserOnly.value).toBe(true);
  });

  it('finds a shared shift in another assignment cell without selecting an unrelated definition', () => {
    const context = userDropContext('assignment', [], [assignmentEvent(258, 21)]);
    context.model.cells.push({
      resourceId: 'assignment-definition-21',
      date: '2026-07-13',
      payload: { shiftEvents: [shiftEvent()] },
      groups: [],
    });

    calendarDropUserOnAssignmentResourceAction.execute(context, {
      featureFlags: {},
      permissions: [Permissions.AssignmentsCreate, Permissions.AssignmentsAssign],
    });

    expect(calendarSchedulingAssignmentModalMode.value).toBe('create');
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBe(20);
    expect(calendarSchedulingAssignmentModalEntryId.value).toBeUndefined();
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('does not select an arbitrary assignment if duplicate entries appear in calendar data', () => {
    const context = userDropContext('assignment', [shiftEvent()], [assignmentEvent(257, 20), assignmentEvent(258, 20)]);
    calendarDropUserOnAssignmentResourceAction.execute(context, {
      featureFlags: {},
      permissions: [Permissions.AssignmentsEdit, Permissions.AssignmentsAssign],
    });

    expect(calendarSchedulingAssignmentModalEntryId.value).toBeUndefined();
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toBeUndefined();
  });

  it('ignores cancelled shifts and refuses unauthorized user drops', () => {
    const cancelledShift = { ...shiftEvent(), statusTypeCode: 'cancelled' };
    const context = userDropContext('assignment', [cancelledShift]);
    const readOnly = { featureFlags: {}, permissions: [Permissions.AssignmentsView] };
    expect(calendarDropUserOnAssignmentResourceAction.isAvailable?.(context, readOnly)).toBe(false);
    calendarDropUserOnAssignmentResourceAction.execute(context, readOnly);
    expect(calendarSchedulingResourceActionDate.value).toBeUndefined();

    calendarDropUserOnAssignmentResourceAction.execute(context, {
      featureFlags: {},
      permissions: [Permissions.ShiftsCreateAndAssign],
    });
    expect(calendarSchedulingResourceActionDate.value).toBe('2026-07-13');
  });

  it('opens the shift modal from the assignment view schedule staff action', async () => {
    await calendarScheduleStaffAction.execute(
      {
        panel: {
          label: 'TEAM',
          actionId: 'calendar-scheduling.schedule-staff',
          actionLabel: 'Schedule staff',
          items: [],
        },
        actionId: 'calendar-scheduling.schedule-staff',
        model: {
          timeZone: 'America/Vancouver',
          days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
          primaryColumn: {
            label: 'ASSIGNMENTS',
            resources: [],
          },
          cells: [],
        },
      },
      writableRuntimeContext,
    );

    expect(calendarSchedulingResourceActionDate.value).toBe('2026-07-13');
    expect(calendarSchedulingResourceActionResource.value).toBeUndefined();
  });

  it('keeps create actions unavailable and defensively rejects execution without write permission', async () => {
    const context = {
      panel: {
        label: 'ASSIGNMENTS',
        actionId: calendarSchedulingActionIds.addAssignment,
        actionLabel: 'Add Assignment',
        items: [],
      },
      actionId: calendarSchedulingActionIds.addAssignment,
      model: {
        timeZone: 'America/Vancouver',
        days: [{ date: '2026-07-27', label: 'Mon, Jul 27' }],
        primaryColumn: { label: 'TEAM', resources: [] },
        cells: [],
      },
    };
    const readOnlyContext = { featureFlags: {}, permissions: [Permissions.AssignmentsView] };

    expect(calendarAddAssignmentAction.isAvailable?.(context, readOnlyContext)).toBe(false);
    await calendarAddAssignmentAction.execute(context, readOnlyContext);
    expect(calendarSchedulingAssignmentModalDate.value).toBeUndefined();
  });
});

function shiftEvent(): CalendarSchedulingEvent {
  return {
    id: 'scheduling.shift-entry.44',
    type: 'scheduling.shift',
    sourceModule: 'scheduling',
    title: 'Shared shift',
    start: '2026-07-13T16:00:00Z',
    end: '2026-07-14T00:00:00Z',
    locationId: 12,
    statusTypeCode: 'draft',
    resourceIds: ['user-1', 'user-2'],
    metadata: { shiftEntryId: '44', userIds: ['user-1', 'user-2'] },
  };
}

function assignmentEvent(entryId: number, definitionId: number, seriesId?: number): CalendarSchedulingEvent {
  return {
    id: `scheduling.assignment-entry.${entryId}`,
    type: 'scheduling.assignment',
    sourceModule: 'scheduling',
    title: `Assignment ${entryId}`,
    start: '2026-07-13T16:00:00Z',
    end: '2026-07-14T00:00:00Z',
    locationId: 12,
    statusTypeCode: 'draft',
    metadata: {
      assignmentEntryId: String(entryId),
      assignmentDefinitionId: String(definitionId),
      assignmentSeriesId: seriesId === undefined ? undefined : String(seriesId),
    },
  };
}

function userDropContext(
  target: 'user' | 'assignment',
  shifts: CalendarEventBase[] = [],
  assignments: CalendarEventBase[] = [],
): CalendarMatrixDropActionContext {
  const isAssignment = target === 'assignment';
  const resourceId = isAssignment ? 'assignment-definition-20' : 'user-1';
  return {
    drag: {
      source: isAssignment ? 'side-panel' : 'resource-row',
      itemId: 'user-1',
      itemType: 'user',
      payload: { title: 'Alex Alpha' },
    },
    drop: { resourceId, resourceType: target, date: '2026-07-13' },
    model: {
      timeZone: 'America/Vancouver',
      days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
      primaryColumn: {
        label: isAssignment ? 'ASSIGNMENTS' : 'TEAM',
        resources: [
          {
            id: resourceId,
            type: target,
            title: 'Target',
            ...(isAssignment ? { assignmentDefinitionId: 20, locationId: 12 } : {}),
          },
        ],
      },
      cells: [
        {
          resourceId,
          date: '2026-07-13',
          headers: isAssignment ? [] : shifts.map((event) => ({ text: 'Shift', payload: event })),
          payload: isAssignment ? { shiftEvents: shifts } : undefined,
          groups: [{ id: 'assignments', events: assignments.map((event) => ({ event })) }],
        },
      ],
    },
  };
}
