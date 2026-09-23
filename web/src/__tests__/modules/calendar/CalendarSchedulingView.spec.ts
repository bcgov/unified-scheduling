import { defineComponent, h, type PropType } from 'vue';
import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CalendarSchedulingView from '@/modules/scheduling/CalendarSchedulingView.vue';
import CalendarSchedulingAssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';
import CalendarSchedulingAddResourceModal from '@/modules/scheduling/CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingAssignmentDefinitionCreateModal from '@/modules/scheduling/CalendarSchedulingAssignmentDefinitionCreateModal.vue';
import CalendarSchedulingShiftDetailModal from '@/modules/scheduling/CalendarSchedulingShiftDetailModal.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { Permissions } from '@/api-access/generated/models';
import type { CalendarConflict } from '@/modules/calendar/calendarTypes';
import type {
  CalendarMatrixEventBlockActionEvent,
  CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import {
  calendarSchedulingConflictEventId,
  calendarSchedulingConflictHeaderId,
  closeCalendarSchedulingAssignmentModal,
  closeCalendarSchedulingEventDetail,
  closeCalendarSchedulingExistingShiftChoice,
  closeCalendarSchedulingResourceActionModal,
  showCalendarSchedulingAssignmentModal,
  showCalendarSchedulingExistingShiftChoice,
  showCalendarSchedulingResourceActionModal,
} from '@/modules/scheduling/calendarSchedulingState';

const api = vi.hoisted(() => ({
  execute: vi.fn(),
  postOverride: vi.fn(),
}));

vi.mock('@/api-access/generated/calendar/calendar', () => ({
  postApiCalendarConflictsOverrides: (...args: unknown[]) => {
    api.postOverride(...args);
    return { error: { value: null }, execute: api.execute };
  },
}));

const model: CalendarMatrixViewModel = {
  days: [],
  primaryColumn: { label: 'TEAM', resources: [] },
  cells: [],
  timeZone: 'America/Vancouver',
};

describe('CalendarSchedulingView', () => {
  afterEach(() => {
    closeCalendarSchedulingAssignmentModal();
    closeCalendarSchedulingEventDetail();
    closeCalendarSchedulingExistingShiftChoice();
    closeCalendarSchedulingResourceActionModal();
    calendarSchedulingConflictEventId.value = undefined;
    calendarSchedulingConflictHeaderId.value = undefined;
    vi.clearAllMocks();
  });

  it('forwards assignment action context to the assignment modal', async () => {
    showCalendarSchedulingAssignmentModal('2026-08-24', {
      mode: 'edit',
      editScope: 'series',
      assignmentEntryId: 11,
      assignmentSeriesId: 12,
      assignmentDefinitionId: 3,
      shiftEntryIds: [21, 22],
    });

    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingView, {
      props: { model },
      attachTo: document.body,
      global: {
        plugins: app.mountPlugins,
        stubs: {
          CalendarMatrixView: { template: '<div><slot /></div>' },
          CalendarSchedulingAssignmentModal: true,
        },
      },
    });

    expect(wrapper.findComponent(CalendarSchedulingAssignmentModal).props()).toMatchObject({
      mode: 'edit',
      editScope: 'series',
      initialDate: '2026-08-24',
      assignmentEntryId: 11,
      assignmentSeriesId: 12,
      initialAssignmentDefinitionId: 3,
      initialShiftEntryIds: [21, 22],
      timeZone: 'America/Vancouver',
    });

    wrapper.unmount();
  });

  it('forwards assignment drop context to the add-resource modal', async () => {
    const assignmentEvent = {
      id: 'assignment-entry-251',
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Court Room Monitor',
      start: '2026-08-24T16:00:00Z',
    };
    showCalendarSchedulingResourceActionModal({ id: 'user-1', type: 'user', title: 'Alex Alpha' }, '2026-08-24', {
      assignmentEntryId: 251,
      assignmentEvents: [assignmentEvent],
    });

    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingView, {
      props: { model },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          CalendarMatrixView: { template: '<div><slot /></div>' },
          CalendarSchedulingAddResourceModal: true,
        },
      },
    });

    expect(wrapper.findComponent(CalendarSchedulingAddResourceModal).props()).toMatchObject({
      initialDate: '2026-08-24',
      initialAssignmentEntryId: 251,
      initialAssignmentEvents: [assignmentEvent],
      timeZone: 'America/Vancouver',
    });

    wrapper.unmount();
  });

  it('opens Assignment Definition details from its sidebar pencil action', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingView, {
      props: { model },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          CalendarMatrixView: {
            emits: ['sidePanelItemClick'],
            template:
              '<button class="definition-item" @click="$emit(\'sidePanelItemClick\', item)">Court Coverage</button>',
            data: () => ({
              item: {
                id: 'assignment-definition-7',
                type: 'assignment',
                title: 'Court Coverage',
                payload: { assignmentDefinitionId: 7 },
              },
            }),
          },
          CalendarSchedulingAssignmentDefinitionCreateModal: true,
        },
      },
    });

    await wrapper.get('.definition-item').trigger('click');

    expect(wrapper.findComponent(CalendarSchedulingAssignmentDefinitionCreateModal).props()).toMatchObject({
      assignmentDefinitionId: 7,
      mode: 'view',
    });

    wrapper.unmount();
  });

  it('opens an existing Shift from an assignment drop directly at event scope', async () => {
    const shiftEvent = {
      id: 'shift-entry-42',
      type: 'scheduling.shift',
      sourceModule: 'scheduling',
      title: 'Alex Alpha',
      start: '2026-08-24T16:00:00Z',
      metadata: { shiftEntryId: '42', shiftSeriesId: '202' },
    };
    showCalendarSchedulingExistingShiftChoice({
      shiftEvent,
      resource: { id: 'user-1', type: 'user', title: 'Alex Alpha' },
      date: '2026-08-24',
      assignmentEntryId: 251,
      assignmentEvents: [],
    });

    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingView, {
      props: {
        model,
        runtimeContext: { featureFlags: {}, permissions: [Permissions.ShiftsEdit] },
      },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          CalendarMatrixView: { template: '<div><slot /></div>' },
          CalendarSchedulingShiftDetailModal: true,
        },
      },
    });

    const editButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Edit existing shift'),
    );
    editButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await wrapper.vm.$nextTick();

    expect(wrapper.findComponent(CalendarSchedulingShiftDetailModal).props()).toMatchObject({
      event: shiftEvent,
      initialOpenScope: 'event',
    });

    wrapper.unmount();
  });

  it('opens the selected conflict and refreshes the calendar after an override', async () => {
    api.execute.mockResolvedValue(undefined);
    const event = createEvent();
    const firstConflict = createConflict(101, 102, 'Second assignment', 'user-1');
    const selectedConflict = createConflict(101, 103, 'Third assignment', 'user-1');
    const conflictModel = createModel(event, [firstConflict, selectedConflict]);
    const app = await createTestApp({ loadConfig: false });
    const calendarStore = useCalendarStore(app.pinia);
    const wrapper = mount(CalendarSchedulingView, {
      props: { model: conflictModel },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          CalendarMatrixView: createMatrixViewStub(),
          CalendarMatrixEventBlock: createEventBlockStub(),
          CalendarSchedulingShiftDetailModal: true,
          CalendarSchedulingAddResourceModal: true,
          CalendarConflictDetailModal: createConflictDetailStub(),
          VIcon: true,
        },
      },
    });

    await wrapper.findAll('[data-test="event-conflict-trigger"]')[1]!.trigger('click');
    expect(wrapper.findAll('.calendar-scheduling-conflict-overlay')).toHaveLength(1);
    await wrapper.findAll('.calendar-scheduling-conflict-overlay__resolve')[1]!.trigger('click');

    const detail = wrapper.getComponent({ name: 'CalendarConflictDetailModalStub' });
    expect(detail.props('conflict')).toEqual(selectedConflict);
    expect(detail.props('currentEventId')).toBe(101);

    detail.vm.$emit('override', 'Approved coverage');
    await flushPromises();

    expect(api.postOverride).toHaveBeenCalledWith(
      expect.objectContaining({ firstEventId: 101, secondEventId: 103, note: 'Approved coverage' }),
      expect.anything(),
    );
    expect(calendarStore.refreshNonce).toBe(1);
  });

  it('opens the assignment edit modal from a conflict event edit action', async () => {
    const event = createEvent();
    const conflict = createConflict(101, 102, 'Second assignment', 'user-1');
    const conflictModel = createModel(event, [conflict]);
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingView, {
      props: { model: conflictModel },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          CalendarMatrixView: createMatrixViewStub(),
          CalendarMatrixEventBlock: createEventBlockStub(),
          CalendarSchedulingAssignmentModal: true,
          CalendarSchedulingShiftDetailModal: true,
          CalendarSchedulingAddResourceModal: true,
          CalendarConflictDetailModal: createConflictDetailStub(),
          VIcon: true,
        },
      },
    });

    await wrapper.findAll('[data-test="event-conflict-trigger"]')[1]!.trigger('click');
    await wrapper.get('.calendar-scheduling-conflict-overlay__resolve').trigger('click');
    wrapper.getComponent({ name: 'CalendarConflictDetailModalStub' }).vm.$emit('editEvent', conflict.entry);
    await wrapper.vm.$nextTick();

    expect(wrapper.findComponent(CalendarSchedulingAssignmentModal).props()).toMatchObject({
      mode: 'edit',
      editScope: 'event',
      initialDate: '2025-01-13',
      assignmentEntryId: 201,
    });
    expect(wrapper.findComponent(CalendarSchedulingShiftDetailModal).exists()).toBe(false);
    expect(wrapper.findComponent({ name: 'CalendarConflictDetailModalStub' }).exists()).toBe(false);
  });

  it('opens a shift-header conflict using the linked assignment event as the current participant', async () => {
    const conflict = createConflict(101, 102, 'Second assignment', 'user-1');
    const shift: CalendarSchedulingEvent = {
      id: 'shift-entry-41',
      type: 'scheduling.shift',
      sourceModule: 'scheduling',
      title: 'Day shift',
      start: '2025-01-13T09:00:00Z',
      end: '2025-01-13T17:00:00Z',
      metadata: { eventId: 501, shiftEntryId: '41', userIds: ['user-1'] },
    };
    const conflictModel: CalendarMatrixViewModel = {
      timeZone: 'America/Vancouver',
      days: [{ date: '2025-01-13', label: 'Monday' }],
      primaryColumn: { label: 'People', resources: [] },
      cells: [
        {
          resourceId: 'user-1',
          date: '2025-01-13',
          headers: [
            {
              id: shift.id,
              text: '9:00 AM - 5:00 PM',
              payload: shift,
              conflicts: [{ conflict, currentEventId: 101 }],
            },
          ],
          groups: [{ id: 'assignments', events: [] }],
        },
      ],
    };
    calendarSchedulingConflictHeaderId.value = shift.id;
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingView, {
      props: { model: conflictModel },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          CalendarMatrixView: createHeaderMatrixViewStub(),
          CalendarMatrixCellHeader: true,
          CalendarSchedulingShiftDetailModal: true,
          CalendarSchedulingAddResourceModal: true,
          CalendarConflictDetailModal: createConflictDetailStub(),
          VIcon: true,
        },
      },
    });

    expect(wrapper.text()).toContain('Second assignment');
    await wrapper.get('.calendar-scheduling-conflict-overlay__resolve').trigger('click');

    const detail = wrapper.getComponent({ name: 'CalendarConflictDetailModalStub' });
    expect(detail.props('conflict')).toEqual(conflict);
    expect(detail.props('currentEventId')).toBe(101);
  });
});

function createMatrixViewStub() {
  return defineComponent({
    name: 'CalendarMatrixViewStub',
    props: {
      model: { type: Object as PropType<CalendarMatrixViewModel>, required: true },
    },
    setup(props, { slots }) {
      return () =>
        h(
          'div',
          props.model.cells.flatMap((cell) => {
            const group = cell.groups[0]!;
            return group.events.flatMap((item) =>
              slots['event-block']?.({
                cell,
                event: item.event,
                display: item.display,
                group,
                onEventAction: (payload: CalendarMatrixEventBlockActionEvent) => {
                  calendarSchedulingConflictEventId.value = payload.event.id;
                },
                onEventClick: vi.fn(),
                onDragStart: vi.fn(),
              }),
            );
          }),
        );
    },
  });
}

function createEventBlockStub() {
  return defineComponent({
    name: 'CalendarMatrixEventBlockStub',
    props: {
      event: { type: Object, required: true },
      display: { type: Object, required: false },
    },
    emits: ['eventAction'],
    template: `
      <button
        data-test="event-conflict-trigger"
        @click="$emit('eventAction', {
          event,
          actionId: display.action.actionId,
          actionType: display.action.type,
        })"
      />
    `,
  });
}

function createConflictDetailStub() {
  return defineComponent({
    name: 'CalendarConflictDetailModalStub',
    props: {
      conflict: { type: Object as PropType<CalendarConflict>, required: true },
      currentEventId: { type: Number, required: true },
    },
    emits: ['editEvent', 'override'],
    template: '<div data-test="conflict-detail" />',
  });
}

function createHeaderMatrixViewStub() {
  return defineComponent({
    name: 'CalendarMatrixViewStub',
    props: {
      model: { type: Object as PropType<CalendarMatrixViewModel>, required: true },
    },
    setup(props, { slots }) {
      return () => {
        const cell = props.model.cells[0]!;
        const header = cell.headers![0]!;
        return h(
          'div',
          slots['cell-header']?.({
            cell,
            header,
            onHeaderAction: vi.fn(),
            onHeaderClick: vi.fn(),
          }),
        );
      };
    },
  });
}

function createModel(event: CalendarSchedulingEvent, conflicts: CalendarConflict[]): CalendarMatrixViewModel {
  const display = {
    action: {
      actionId: 'calendar-scheduling.show-conflict',
      icon: 'alert',
      type: 'button' as const,
    },
  };

  return {
    timeZone: 'America/Vancouver',
    days: [{ date: '2025-01-13', label: 'Monday' }],
    primaryColumn: { label: 'People', resources: [] },
    cells: [
      {
        resourceId: 'user-2',
        date: '2025-01-13',
        groups: [
          {
            id: 'assignments',
            events: [{ event: { ...event }, display, conflicts: [] }],
          },
        ],
      },
      {
        resourceId: 'user-1',
        date: '2025-01-13',
        groups: [
          {
            id: 'assignments',
            events: [
              {
                event,
                display,
                conflicts: conflicts.map((conflict) => ({ conflict, currentEventId: event.metadata.eventId! })),
              },
            ],
          },
        ],
      },
    ],
  };
}

function createEvent(): CalendarSchedulingEvent {
  return {
    id: 'assignment-entry-201',
    type: 'scheduling.assignment',
    sourceModule: 'scheduling',
    title: 'First assignment',
    start: '2025-01-13T10:00:00Z',
    end: '2025-01-13T12:00:00Z',
    metadata: { eventId: 101, assignmentEntryId: '201' },
  };
}

function createConflict(
  currentEventId: number,
  conflictingEventId: number,
  conflictingTitle: string,
  resourceId: string,
): CalendarConflict {
  return {
    id: `conflict:${currentEventId}:${conflictingEventId}:${resourceId}`,
    entry: {
      eventId: currentEventId,
      sourceModule: 'scheduling',
      title: 'First assignment',
      start: '2025-01-13T10:00:00Z',
      end: '2025-01-13T12:00:00Z',
      sourceEntityId: null,
      timeZoneId: null,
    },
    overlaps: {
      eventId: conflictingEventId,
      sourceModule: 'scheduling',
      title: conflictingTitle,
      start: '2025-01-13T11:00:00Z',
      end: '2025-01-13T13:00:00Z',
      sourceEntityId: null,
      timeZoneId: null,
    },
    resourceId,
    overlapStart: '2025-01-13T11:00:00Z',
    overlapEnd: '2025-01-13T12:00:00Z',
    isOverridden: false,
    overrideNote: null,
    createdById: null,
    createdOn: null,
    updatedById: null,
    updatedOn: null,
  };
}
