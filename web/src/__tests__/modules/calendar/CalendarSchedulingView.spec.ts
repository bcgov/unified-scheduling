import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import CalendarSchedulingView from '@/modules/scheduling/CalendarSchedulingView.vue';
import CalendarSchedulingAssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';
import CalendarSchedulingAddResourceModal from '@/modules/scheduling/CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingShiftDetailModal from '@/modules/scheduling/CalendarSchedulingShiftDetailModal.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import {
  closeCalendarSchedulingAssignmentModal,
  closeCalendarSchedulingEventDetail,
  closeCalendarSchedulingExistingShiftChoice,
  closeCalendarSchedulingResourceActionModal,
  showCalendarSchedulingExistingShiftChoice,
  showCalendarSchedulingAssignmentModal,
  showCalendarSchedulingResourceActionModal,
} from '@/modules/scheduling/calendarSchedulingState';

const model = {
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

    const modal = wrapper.findComponent(CalendarSchedulingAssignmentModal);
    expect(modal.props()).toMatchObject({
      mode: 'edit',
      editScope: 'series',
      initialDate: '2026-08-24',
      assignmentEntryId: 11,
      assignmentSeriesId: 12,
      initialAssignmentDefinitionId: 3,
      initialShiftEntryIds: [21, 22],
      timeZone: 'America/Vancouver',
    });
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
      props: { model },
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
  });
});
