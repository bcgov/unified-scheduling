import { createTestApp } from '@/__tests__/helpers/createTestApp';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
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

function createLeaveEvent(): CalendarEventBase {
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
  } as CalendarEventBase;
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
      useLeaveTypeOptions: () => ({ filterLeaveTypeOptionsByDate: () => [] }),
    }));
  });

  afterEach(() => {
    document.body.innerHTML = '';
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
          UaModal: true,
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
    await vm.handleSave();
    await flushPromises();

    expect(mocks.updateLeaveEntry).toHaveBeenCalledWith(42, expect.objectContaining({ leaveSeriesId: 77 }));

    wrapper.unmount();
  });
});
