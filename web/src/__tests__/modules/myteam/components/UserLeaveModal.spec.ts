import UserLeaveModal from '@/modules/myteam/components/UserLeaveModal.vue';
import type { LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { createLeaveEntryMock, getLeaveEntryMock, getLeaveSeriesMock, updateLeaveEntryMock } = vi.hoisted(() => ({
  createLeaveEntryMock: vi.fn(),
  getLeaveEntryMock: vi.fn(),
  getLeaveSeriesMock: vi.fn(),
  updateLeaveEntryMock: vi.fn(),
}));

vi.mock('@/modules/leave/api/leaveApi', () => ({
  createLeaveEntry: createLeaveEntryMock,
  createLeaveSeries: vi.fn(),
  getLeaveEntry: getLeaveEntryMock,
  getLeaveSeries: getLeaveSeriesMock,
  updateLeaveEntry: updateLeaveEntryMock,
  updateLeaveSeries: vi.fn(),
}));

vi.mock('@/stores/LocationsStore', () => ({
  useLocationsStore: () => ({ selectedLocationId: '', entitiesMap: {} }),
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
  emits: ['update:modelValue'],
  template: '<div data-test="form">{{ JSON.stringify(modelValue) }}</div>',
});

describe('UserLeaveModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    createLeaveEntryMock.mockResolvedValue({ error: ref(null) });
    updateLeaveEntryMock.mockResolvedValue({ error: ref(null) });
  });

  it.each([
    { startAtUtc: '2026-06-10T17:00:00Z', endAtUtc: '2026-06-10T18:00:00Z', endTime: '11:00' },
    { startAtUtc: '2026-06-10T17:00:00Z', endAtUtc: null, endTime: null },
    { startAtUtc: '2026-06-11T02:00:00Z', endAtUtc: null, endTime: null },
  ])(
    'preserves the loaded end when saving unrelated changes ($startAtUtc, $endAtUtc)',
    async ({ startAtUtc, endAtUtc, endTime }) => {
      getLeaveEntryMock.mockResolvedValue({
        data: ref({
          id: 42,
          userIds: ['current-user'],
          leaveTypeId: 9,
          startAtUtc,
          endAtUtc,
          timeZoneId: 'America/Vancouver',
          notes: 'Latest server note',
          locationId: 3,
          leaveSeriesId: 77,
        }),
        error: ref(null),
      });
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

      const form = wrapper.getComponent(LeaveFormStub);
      const modelValue = form.props('modelValue') as LeaveFormData;
      expect(modelValue.endTime).toBe(endTime);
      form.vm.$emit('update:modelValue', { ...modelValue, notes: 'Edited unrelated notes' });
      await flushPromises();

      await wrapper
        .findAll('button')
        .find((button) => button.text() === 'Save')!
        .trigger('click');
      await flushPromises();
      expect(updateLeaveEntryMock).toHaveBeenCalledWith(
        42,
        expect.objectContaining({
          notes: 'Edited unrelated notes',
          leaveTypeId: 9,
          leaveSeriesId: 77,
          startAtUtc,
          endAtUtc,
        }),
      );
      expect(wrapper.emitted('save')).toHaveLength(1);
      wrapper.unmount();
    },
  );

  it('retains default start and end times for new leave creation', async () => {
    const wrapper = mount(UserLeaveModal, {
      props: { user: { id: 'current-user', homeLocationId: 3 } as never },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaBtn: UaBtnStub,
          UaAlert: UaAlertStub,
          LeaveFormComponent: LeaveFormStub,
        },
      },
    });
    await flushPromises();
    expect(getLeaveEntryMock).not.toHaveBeenCalled();
    expect(getLeaveSeriesMock).not.toHaveBeenCalled();
    const form = wrapper.getComponent(LeaveFormStub);
    const modelValue = form.props('modelValue') as LeaveFormData;
    expect(modelValue.startTime).toBe('09:00');
    expect(modelValue.endTime).toBe('17:00');
    form.vm.$emit('update:modelValue', { ...modelValue, date: '2026-06-10', leaveType: 9 });
    await flushPromises();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await flushPromises();
    expect(createLeaveEntryMock).toHaveBeenCalledWith(
      expect.objectContaining({
        startAtUtc: '2026-06-10T16:00:00Z',
        endAtUtc: '2026-06-11T00:00:00Z',
      }),
    );
    expect(wrapper.emitted('save')).toHaveLength(1);
    wrapper.unmount();
  });
});
