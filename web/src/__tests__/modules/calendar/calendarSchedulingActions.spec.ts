import { beforeEach, describe, expect, it } from 'vitest';
import type { CalendarMatrixDropActionContext } from '@/modules/calendar/registry/calendarActionRegistryTypes';
import {
  calendarAddAssignmentAction,
  calendarAddResourceAction,
  calendarAddAssignmentResourceAction,
  calendarDropAction,
  calendarDropUserOnAssignmentResourceAction,
  calendarScheduleStaffAction,
  calendarSchedulingEventDetailAction,
} from '@/modules/scheduling/calendarSchedulingActions';
import {
  calendarSchedulingAssignmentModalAssignmentDefinitionId,
  calendarSchedulingAssignmentModalDate,
  calendarSchedulingAssignmentModalEntryId,
  calendarSchedulingAssignmentModalMode,
  calendarSchedulingAssignmentModalShiftEntryIds,
  calendarSchedulingResourceActionDate,
  calendarSchedulingResourceActionAssignmentEntryId,
  calendarSchedulingResourceActionAssignmentEvents,
  calendarSchedulingResourceActionResource,
  closeCalendarSchedulingAssignmentModal,
  closeCalendarSchedulingResourceActionModal,
} from '@/modules/scheduling/calendarSchedulingState';

describe('calendarSchedulingActions', () => {
  beforeEach(() => {
    closeCalendarSchedulingAssignmentModal();
    closeCalendarSchedulingResourceActionModal();
  });

  it('opens the add assignment modal on today when today is in the displayed range', async () => {
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
      { featureFlags: {} },
    );

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-31');
  });

  it('falls back to the first displayed day when today is outside the displayed range', async () => {
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
          days: [
            { date: '2026-08-03', label: 'Mon, Aug 3' },
            { date: '2026-08-04', label: 'Tue, Aug 4' },
          ],
          primaryColumn: {
            label: 'TEAM',
            resources: [],
          },
          cells: [],
        },
      },
      { featureFlags: {} },
    );

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-08-03');
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
      featureFlags: {},
    });

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-12');
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBe(20);
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('edits the existing assignment from the dragged definition entries when it is not rendered in a cell', async () => {
    const context: CalendarMatrixDropActionContext = {
      drag: {
        source: 'side-panel',
        itemId: 'assignment-definition-20',
        itemType: 'assignment',
        payload: {
          title: 'Court Room Monitor',
          assignmentDefinitionId: 20,
          entries: [
            {
              id: 90,
              title: 'Court Room Monitor',
              startAtUtc: '2026-07-12T16:00:00Z',
              endAtUtc: '2026-07-12T17:00:00Z',
            },
          ],
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

    await calendarDropAction.execute(context, { featureFlags: {} });

    expect(calendarSchedulingAssignmentModalMode.value).toBe('edit');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(90);
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBeUndefined();
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('edits an assignment belonging to another user when the drop target has no shift', async () => {
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

    await calendarDropAction.execute(context, { featureFlags: {} });

    expect(calendarSchedulingAssignmentModalMode.value).toBe('edit');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(90);
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([]);
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

    await calendarDropAction.execute(context, { featureFlags: {} });

    expect(calendarSchedulingAssignmentModalMode.value).toBe('edit');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(90);
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
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
      featureFlags: {},
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
      featureFlags: {},
    });

    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([44]);
  });

  it('opens the assignment modal with no shift links when dropping on a user row with no shifts', async () => {
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
        resourceId: 'user-test',
        resourceType: 'user',
        date: '2026-07-13',
      },
      model: {
        days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
        primaryColumn: {
          label: 'TEAM',
          resources: [{ id: 'user-test', type: 'user', title: 'Test Test' }],
        },
        cells: [
          {
            resourceId: 'user-test',
            date: '2026-07-13',
            headers: [],
            groups: [],
          },
        ],
      },
    };

    await calendarDropAction.execute(context, {
      featureFlags: {},
    });

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-13');
    expect(calendarSchedulingAssignmentModalAssignmentDefinitionId.value).toBe(20);
    expect(calendarSchedulingAssignmentModalShiftEntryIds.value).toEqual([]);
  });

  it('opens the assignment view modal for scheduling assignment event details', () => {
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
        },
      },
    } as never);

    expect(calendarSchedulingAssignmentModalDate.value).toBe('2026-07-14');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(200);
    expect(calendarSchedulingAssignmentModalMode.value).toBe('view');
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
          days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
          primaryColumn: {
            label: 'ASSIGNMENTS',
            resources: [],
          },
          cells: [],
        },
      },
      { featureFlags: {} },
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
          days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
          primaryColumn: {
            label: 'TEAM',
            resources: [],
          },
          cells: [],
        },
      },
      { featureFlags: {} },
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
      { featureFlags: {} },
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
          days: [{ date: '2026-07-13', label: 'Mon, Jul 13' }],
          primaryColumn: {
            label: 'ASSIGNMENTS',
            resources: [],
          },
          cells: [],
        },
      },
      { featureFlags: {} },
    );

    expect(calendarSchedulingResourceActionDate.value).toBe('2026-07-13');
    expect(calendarSchedulingResourceActionResource.value).toBeUndefined();
  });
});
