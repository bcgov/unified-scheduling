import UserTimeOffModal from '@/modules/myteam/components/UserTimeOffModal.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getTimeOffEntryMock, getTimeOffSeriesMock, updateTimeOffEntryMock } = vi.hoisted(() => ({
  getTimeOffEntryMock: vi.fn(),
  getTimeOffSeriesMock: vi.fn(),
  updateTimeOffEntryMock: vi.fn(),
}));

vi.mock('@/modules/timeoff/api/timeOffApi', () => ({
  createTimeOffEntry: vi.fn(),
  createTimeOffSeries: vi.fn(),
  getTimeOffEntry: getTimeOffEntryMock,
  getTimeOffSeries: getTimeOffSeriesMock,
  updateTimeOffEntry: updateTimeOffEntryMock,
  updateTimeOffSeries: vi.fn(),
}));

vi.mock('@/stores/LocationsStore', () => ({
  useLocationsStore: () => ({ selectedLocationId: '', entitiesMap: {} }),
}));

vi.mock('@/modules/timeoff/timeOffRequest', () => ({
  buildTimeOffRequest: vi.fn((formData: Record<string, unknown>) => ({
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

const TimeOffFormStub = defineComponent({
  name: 'TimeOffFormComponent',
  props: ['modelValue'],
  template: '<div data-test="form">{{ JSON.stringify(modelValue) }}</div>',
});

describe('UserTimeOffModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getTimeOffEntryMock.mockResolvedValue({
      data: ref({
        id: 42,
        assignedUserIds: ['current-user'],
        leaveTypeId: 9,
        startAtUtc: '2026-06-10T17:00:00Z',
        endAtUtc: '2026-06-10T18:00:00Z',
        timeZoneId: 'America/Vancouver',
        notes: 'Latest server note',
        locationId: 3,
      }),
      error: ref(null),
    });
    updateTimeOffEntryMock.mockResolvedValue({ error: ref(null) });
  });

  it('loads the current entry before enabling an event edit', async () => {
    const wrapper = mount(UserTimeOffModal, {
      props: {
        user: { id: 'current-user', homeLocationId: 3 } as never,
        scope: 'event',
        entry: {
          id: 42,
          assignedUserIds: ['current-user'],
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
          TimeOffFormComponent: TimeOffFormStub,
        },
      },
    });

    expect(getTimeOffEntryMock).toHaveBeenCalledWith(42);
    await flushPromises();
    expect(wrapper.get('[data-test="form"]').text()).toContain('Latest server note');
    expect(wrapper.get('[data-test="form"]').text()).not.toContain('Stale note');

    await wrapper.findAll('button').find((button) => button.text() === 'Save')!.trigger('click');
    await flushPromises();
    expect(updateTimeOffEntryMock).toHaveBeenCalledWith(42, expect.objectContaining({
      notes: 'Latest server note',
      leaveType: 9,
    }));
  });
});
