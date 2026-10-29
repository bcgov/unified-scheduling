import { createTestApp } from '@/__tests__/helpers/createTestApp';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import type { LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { ref } from 'vue';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getLeaveEntry: vi.fn(),
  getLeaveSeries: vi.fn(),
  updateLeaveEntry: vi.fn(),
  updateLeaveSeries: vi.fn(),
  deleteLeaveEntry: vi.fn(),
  deleteLeaveSeries: vi.fn(),
  hasPermission: vi.fn(() => true),
}));

function createLeaveEvent(): CalendarSchedulingEvent {
  return {
    id: 'leave-entry.42',
    type: 'scheduling.leave',
    sourceModule: 'scheduling',
    title: 'Leave',
    start: '2026-06-10T17:00:00Z',
    end: '2026-06-10T18:00:00Z',
    timeZoneId: 'America/Vancouver',
    statusTypeCode: 'Draft',
    locationId: 3,
    resourceIds: ['00000000-0000-0000-0000-000000000001'],
    metadata: {
      leaveEntryId: '42',
      leaveSeriesId: '77',
      leaveTypeId: 9,
      userIds: ['00000000-0000-0000-0000-000000000001'],
    },
  };
}

describe('CalendarSchedulingLeaveDetailModal', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    mocks.getLeaveEntry.mockResolvedValue({
      data: ref({
        id: 42,
        leaveSeriesId: 77,
        userIds: ['00000000-0000-0000-0000-000000000001'],
        leaveTypeId: 9,
        startAtUtc: '2026-06-10T17:00:00Z',
        endAtUtc: '2026-06-10T18:00:00Z',
        timeZoneId: 'America/Vancouver',
        locationId: 3,
      }),
      error: ref(null),
    });
    mocks.updateLeaveEntry.mockResolvedValue({ error: ref(null) });
    vi.doMock('@/modules/leave/api/leaveApi', () => ({
      getLeaveEntry: mocks.getLeaveEntry,
      getLeaveSeries: mocks.getLeaveSeries,
      updateLeaveEntry: mocks.updateLeaveEntry,
      updateLeaveSeries: mocks.updateLeaveSeries,
      deleteLeaveEntry: mocks.deleteLeaveEntry,
      deleteLeaveSeries: mocks.deleteLeaveSeries,
    }));
    vi.doMock('@/composables/useAccessControl', () => ({
      useAccessControl: () => ({ hasPermission: mocks.hasPermission }),
    }));
    vi.doMock('@/stores/LocationsStore', () => ({
      useLocationsStore: () => ({ entitiesMap: {}, selectOptions: [] }),
    }));
    vi.doMock('@/stores/Users', () => ({
      useUsersStore: () => ({ ensureUsersForLocation: vi.fn().mockResolvedValue([]) }),
    }));
    vi.doMock('@/modules/leave/composables/useLeaveTypeOptions', () => ({
      useLeaveTypeOptions: () => ({
        filterLeaveTypeOptionsByDate: () => [{ code: 9, description: 'Vacation' }],
        leaveTypeOptions: ref([]),
        isLoadingLeaveTypes: ref(false),
        leaveTypesError: ref(''),
      }),
    }));
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  async function mountEntryEditor(loaded: boolean, endAtUtc: string | null) {
    const { default: CalendarSchedulingLeaveDetailModal } =
      await import('@/modules/scheduling/CalendarSchedulingLeaveDetailModal.vue');
    const event = createLeaveEvent();
    event.start = '2026-06-11T02:00:00Z'; // 19:00 in Vancouver, after the default end time.
    event.end = endAtUtc ?? undefined;
    if (!loaded) event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    mocks.getLeaveEntry.mockResolvedValue({
      data: ref({
        id: 42,
        leaveSeriesId: 77,
        userIds: event.resourceIds,
        leaveTypeId: 9,
        startAtUtc: event.start,
        endAtUtc,
        timeZoneId: 'America/Vancouver',
        locationId: 3,
      }),
      error: ref(null),
    });
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingLeaveDetailModal, {
      props: { event },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: { template: '<section><slot name="alerts" /><slot /><slot name="actions" /></section>' },
          LeaveDetailsPanel: true,
        },
      },
    });
    const vm = wrapper.vm as unknown as {
      selectOpenScope: (scope: 'event') => Promise<void>;
      selectTab: (tabId: 'edit') => void;
      editFormData: LeaveFormData;
      handleSave: () => Promise<void>;
    };
    if (loaded) {
      await vm.selectOpenScope('event');
      expect(mocks.getLeaveEntry).toHaveBeenCalledWith(42);
    } else {
      expect(mocks.getLeaveEntry).not.toHaveBeenCalled();
    }
    vm.selectTab('edit');
    await flushPromises();
    return { wrapper, vm };
  }

  it.each([true, false])('preserves a null end when editing unrelated fields (loaded entry: %s)', async (loaded) => {
    const { wrapper, vm } = await mountEntryEditor(loaded, null);
    try {
      expect(vm.editFormData.startTime).toBe('19:00');
      expect(vm.editFormData.endTime).toBeNull();
      expect((wrapper.get('input[aria-label="End Time"]').element as HTMLInputElement).value).toBe('');
      await wrapper.get('textarea#user-leave-notes').setValue('Updated notes');
      await vm.handleSave();
      expect(mocks.updateLeaveEntry).toHaveBeenCalledWith(
        42,
        expect.objectContaining({
          startAtUtc: '2026-06-11T02:00:00Z',
          endAtUtc: null,
          notes: 'Updated notes',
        }),
      );
    } finally {
      wrapper.unmount();
    }
  });

  it.each([true, false])('persists an end selected on an entry that had no end (loaded entry: %s)', async (loaded) => {
    const { wrapper, vm } = await mountEntryEditor(loaded, null);
    try {
      const endSelect = wrapper
        .findAllComponents({ name: 'UaSelect' })
        .find((select) => select.find('input[aria-label="End Time"]').exists())!;
      endSelect.vm.$emit('update:modelValue', '20:00');
      await flushPromises();
      expect(vm.editFormData.endTime).toBe('20:00');
      await vm.handleSave();
      expect(mocks.updateLeaveEntry).toHaveBeenCalledWith(
        42,
        expect.objectContaining({
          endAtUtc: '2026-06-11T03:00:00Z',
        }),
      );
    } finally {
      wrapper.unmount();
    }
  });

  it.each([true, false])(
    'preserves an existing end time when saved without changes (loaded entry: %s)',
    async (loaded) => {
      const { wrapper, vm } = await mountEntryEditor(loaded, '2026-06-11T03:00:00Z');
      try {
        expect(vm.editFormData.endTime).toBe('20:00');
        expect((wrapper.get('input[aria-label="End Time"]').element as HTMLInputElement).value).toBe('8:00 PM');
        await vm.handleSave();
        expect(mocks.updateLeaveEntry).toHaveBeenCalledWith(
          42,
          expect.objectContaining({
            endAtUtc: '2026-06-11T03:00:00Z',
          }),
        );
      } finally {
        wrapper.unmount();
      }
    },
  );

  it('retains the default end-time display for an uninitialized creation form', async () => {
    const { default: LeaveForm } = await import('@/modules/leave/components/LeaveForm.vue');
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(LeaveForm, {
      props: { modelValue: { date: '2026-06-10' } },
      global: { plugins: app.mountPlugins },
    });
    try {
      expect((wrapper.get('input[aria-label="End Time"]').element as HTMLInputElement).value).toBe('5:00 PM');
    } finally {
      wrapper.unmount();
    }
  });

  it('sends the series ID when updating one occurrence from a leave series', async () => {
    const { default: CalendarSchedulingLeaveDetailModal } =
      await import('@/modules/scheduling/CalendarSchedulingLeaveDetailModal.vue');
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingLeaveDetailModal, {
      props: { event: createLeaveEvent() },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaAlert: true,
          UaBtn: true,
          UaModal: { template: '<section><slot /></section>' },
          LeaveDetailsPanel: true,
          LeaveForm: true,
        },
      },
    });
    const vm = wrapper.vm as unknown as {
      selectOpenScope: (scope: 'event') => Promise<void>;
      selectTab: (tabId: 'edit') => void;
      handleSave: () => Promise<void>;
    };

    await vm.selectOpenScope('event');
    vm.selectTab('edit');
    await flushPromises();
    expect(wrapper.getComponent({ name: 'LeaveForm' }).props('showRequired')).toBe(true);
    await vm.handleSave();
    await flushPromises();

    expect(mocks.updateLeaveEntry).toHaveBeenCalledWith(42, expect.objectContaining({ leaveSeriesId: 77 }));

    wrapper.unmount();
  });
});
