import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';

function createShiftEvent(statusTypeCode: string, belongsToSeries = false): CalendarEventBase {
  return {
    id: `shift-${statusTypeCode}`,
    type: 'scheduling.shift',
    sourceModule: 'scheduling',
    title: `${statusTypeCode} shift`,
    start: '2026-07-13T16:00:00Z',
    end: '2026-07-14T00:00:00Z',
    timeZoneId: 'America/Vancouver',
    statusTypeCode,
    locationId: 12,
    resourceIds: ['00000000-0000-0000-0000-000000000001'],
    metadata: {
      shiftEntryId: '42',
      shiftSeriesId: belongsToSeries ? '202' : undefined,
      userIds: ['00000000-0000-0000-0000-000000000001'],
    },
  } as CalendarEventBase;
}

async function mountShiftDetailModal(statusTypeCode: string, belongsToSeries = false) {
  vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () => ({
    loadShiftEntry: vi.fn().mockResolvedValue({
      data: {
        value: {
          id: 42,
          title: `${statusTypeCode} shift`,
          startAtUtc: '2026-07-13T16:00:00Z',
          endAtUtc: '2026-07-14T00:00:00Z',
          timeZoneId: 'America/Vancouver',
          statusTypeCode,
          locationId: 12,
          userIds: ['00000000-0000-0000-0000-000000000001'],
          assignmentLinks: [
            {
              assignmentEntryId: 251,
              userIds: ['00000000-0000-0000-0000-000000000001'],
            },
          ],
        },
      },
      error: { value: null },
    }),
    updateShiftEntry: vi.fn(),
    updateShiftSeries: vi.fn(),
    publishShiftEntry: vi.fn(),
    publishShiftSeries: vi.fn(),
    cancelShiftEntry: vi.fn().mockResolvedValue({ data: { value: null }, error: { value: null } }),
    cancelShiftSeries: vi.fn().mockResolvedValue({ data: { value: null }, error: { value: null } }),
  }));
  vi.doMock('@/api-access/generated/users/users', () => ({
    getApiUsers: vi.fn().mockReturnValue({
      data: {
        value: [
          {
            id: '00000000-0000-0000-0000-000000000001',
            firstName: 'Alex',
            lastName: 'Alpha',
          },
        ],
      },
      error: { value: null },
      execute: vi.fn().mockResolvedValue(undefined),
    }),
  }));
  vi.doMock('@/api-access/generated/shift-assignment/shift-assignment', () => ({
    postApiSchedulingShiftAssignmentsOptions: vi.fn().mockReturnValue({
      data: {
        value: {
          entryOptions: [
            {
              id: 251,
              title: 'Court Coverage',
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              statusTypeCode: 'Draft',
            },
          ],
          seriesOptions: [],
          hasSameDayNonOverlappingAssignments: false,
        },
      },
      error: { value: null },
      response: { value: { ok: true } },
      execute: vi.fn().mockResolvedValue(undefined),
    }),
  }));

  const { default: CalendarSchedulingShiftDetailModal } =
    await import('@/modules/scheduling/CalendarSchedulingShiftDetailModal.vue');
  const app = await createTestApp({ loadConfig: false });
  const locationsStore = useLocationsStore(app.pinia);
  locationsStore.entities = [{ id: 12, name: 'Headquarters' }] as never[];
  locationsStore.setSelectedLocationId(12);

  const wrapper = mount(CalendarSchedulingShiftDetailModal, {
    props: {
      event: createShiftEvent(statusTypeCode, belongsToSeries),
    },
    global: { plugins: app.mountPlugins },
    attachTo: document.body,
  });

  await flushPromises();

  return wrapper;
}

describe('CalendarSchedulingShiftDetailModal', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('keeps draft shifts editable', async () => {
    const wrapper = await mountShiftDetailModal('Draft');

    expect(document.body.textContent).toContain('Edit');
    expect(document.body.textContent).toContain('Assignment(s)');
    expect(document.body.textContent).toContain('Court Coverage');

    const editTab = Array.from(document.querySelectorAll('button')).find((button) => button.textContent === 'Edit');
    editTab?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(document.body.textContent).toContain('Employee');
    expect(document.body.textContent).toContain('Linked assignments');
    expect(document.body.textContent).toContain('Court Coverage');

    wrapper.unmount();
  });

  it('preserves linked assignments when opening one shift entry from a series', async () => {
    const wrapper = await mountShiftDetailModal('Draft', true);

    const eventScopeButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent === 'Only this event',
    );
    eventScopeButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    const editTab = Array.from(document.querySelectorAll('button')).find((button) => button.textContent === 'Edit');
    editTab?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    const vm = wrapper.vm as unknown as {
      editFormData: { assignmentEntryLinks?: Array<{ assignmentEntryId?: number }> };
    };
    expect(vm.editFormData.assignmentEntryLinks).toEqual([expect.objectContaining({ assignmentEntryId: 251 })]);
    expect(document.body.textContent).toContain('Linked assignments');
    expect(document.body.textContent).toContain('Court Coverage');

    wrapper.unmount();
  });

  it.each(['Active', 'Cancelled'])('does not expose editable controls for %s shifts', async (statusTypeCode) => {
    const wrapper = await mountShiftDetailModal(statusTypeCode);
    const vm = wrapper.vm as unknown as {
      activeTab: string;
      selectTab: (tabId: 'edit') => void;
    };

    expect(Array.from(document.querySelectorAll('button')).some((button) => button.textContent === 'Edit')).toBe(false);

    vm.selectTab('edit');
    await flushPromises();

    expect(vm.activeTab).toBe('details');
    expect(document.body.textContent).not.toContain('Linked assignments');

    wrapper.unmount();
  });

  it('shows a published shift message for active shifts', async () => {
    const wrapper = await mountShiftDetailModal('Active');

    expect(document.body.textContent).toContain(
      'This shift has been published, and cannot be edited or deleted, only cancelled',
    );

    wrapper.unmount();
  });

  it('cancels an active shift entry from the Cancel panel', async () => {
    const wrapper = await mountShiftDetailModal('Active');
    const shiftApi = await import('@/modules/scheduling/calendarSchedulingShiftApi');
    const vm = wrapper.vm as unknown as {
      isDeleteConfirmed: boolean;
      selectTab: (tabId: 'delete') => void;
      handleDeleteShift: () => Promise<void>;
    };

    vm.selectTab('delete');
    vm.isDeleteConfirmed = true;
    await vm.handleDeleteShift();

    expect(shiftApi.cancelShiftEntry).toHaveBeenCalledWith(42);
    expect(shiftApi.updateShiftEntry).not.toHaveBeenCalled();

    wrapper.unmount();
  });
});
