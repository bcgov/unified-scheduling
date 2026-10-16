import UserLeaveModal from '@/modules/myteam/components/UserLeaveModal.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getLeaveEntryMock, getLeaveSeriesMock, updateLeaveEntryMock } = vi.hoisted(() => ({
  getLeaveEntryMock: vi.fn(),
  getLeaveSeriesMock: vi.fn(),
  updateLeaveEntryMock: vi.fn(),
}));

vi.mock('@/modules/leave/api/leaveApi', () => ({
  createLeaveEntry: vi.fn(),
  createLeaveSeries: vi.fn(),
  getLeaveEntry: getLeaveEntryMock,
  getLeaveSeries: getLeaveSeriesMock,
  updateLeaveEntry: updateLeaveEntryMock,
  updateLeaveSeries: vi.fn(),
}));

vi.mock('@/stores/LocationsStore', () => ({
  useLocationsStore: () => ({ selectedLocationId: '', entitiesMap: {} }),
}));

vi.mock('@/modules/leave/leaveRequest', () => ({
  buildLeaveRequest: vi.fn((formData: Record<string, unknown>) => ({
    kind: 'entry',
    request: formData,
  })),
}));

const UaModalStub = defineComponent({
  name: 'UaModal',
  template: '<div><slot /><slot name="actions" /></div>',
});

const UaBtnStub = defineComponent({
  name: 'UaBtn',
  emits: ['click'],
  template: '<button @click="$emit(\'click\')"><slot /></button>',
});

const UaAlertStub = defineComponent({ name: 'UaAlert', template: '<div><slot /></div>' });

const LeaveFormStub = defineComponent({
  name: 'LeaveFormComponent',
  props: ['modelValue'],
  template: '<div data-test="form">{{ JSON.stringify(modelValue) }}</div>',
});

describe('UserLeaveModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getLeaveEntryMock.mockResolvedValue({
      data: ref({
        id: 42,
        userIds: ['current-user'],
        leaveTypeId: 9,
        startAtUtc: '2026-06-10T17:00:00Z',
        endAtUtc: '2026-06-10T18:00:00Z',
        timeZoneId: 'America/Vancouver',
        notes: 'Latest server note',
        locationId: 3,
      }),
      error: ref(null),
    });
    updateLeaveEntryMock.mockResolvedValue({ error: ref(null) });
  });

  it('loads the current entry before enabling an event edit', async () => {
    const wrapper = mount(UserLeaveModal, {
      props: {
        user: { id: 'current-user', homeLocationId: 3 } as never,
        scope: 'event',
        entry: {
          id: 42,
          title: 'Leave',
          userIds: ['current-user'],
          leaveTypeId: 1,
          startAtUtc: '2026-01-01T17:00:00Z',
          endAtUtc: '2026-01-01T18:00:00Z',
          timeZoneId: 'America/Vancouver',
          notes: 'Stale note',
        },
      },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaBtn: UaBtnStub,
          UaAlert: UaAlertStub,
          LeaveFormComponent: LeaveFormStub,
        },
      },
    });

    expect(getLeaveEntryMock).toHaveBeenCalledWith(42);
    await flushPromises();
    expect(wrapper.get('[data-test="form"]').text()).toContain('Latest server note');
    expect(wrapper.get('[data-test="form"]').text()).not.toContain('Stale note');

    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await flushPromises();
    expect(updateLeaveEntryMock).toHaveBeenCalledWith(
      42,
      expect.objectContaining({
        notes: 'Latest server note',
        leaveType: 9,
      }),
    );
  });
});
