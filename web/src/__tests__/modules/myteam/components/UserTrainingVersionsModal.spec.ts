import UserTrainingVersionsModal from '@/modules/myteam/components/UserTrainingVersionsModal.vue';
import { mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { afterEach, describe, expect, it, vi } from 'vitest';

const UaModalStub = defineComponent({
  name: 'UaModal',
  emits: ['close'],
  template: '<div><slot /></div>',
});

const UaDataTableStub = defineComponent({
  name: 'UaDataTable',
  props: {
    items: {
      type: Array,
      required: false,
      default: () => [],
    },
  },
  template: `<div class="ua-data-table-stub">rows: {{ items.length }}
    <div v-for="(item, index) in items" :key="index">
      <slot name="item.awardedOn" :item="item" />
      <slot name="item.endingOn" :item="item" />
      <slot name="item.expiryDate" :item="item" />
      <slot name="item.status" :item="item" />
    </div>
  </div>`,
});

const VTabsStub = defineComponent({
  name: 'VTabs',
  props: {
    modelValue: {
      type: String,
      required: false,
      default: 'details',
    },
  },
  emits: ['update:modelValue'],
  template:
    '<div><button data-test="show-details" @click="$emit(\'update:modelValue\', \'details\')">details</button><button data-test="show-history" @click="$emit(\'update:modelValue\', \'history\')">history</button><slot /></div>',
});

const VRowStub = defineComponent({
  name: 'VRow',
  template: '<div><slot /></div>',
});

const VColStub = defineComponent({
  name: 'VCol',
  template: '<div><slot /></div>',
});

const sampleTraining = {
  id: 10,
  userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
  trainingId: 88,
  version: 2,
  trainingCode: 'CPR',
  trainingCategoryName: 'Medical',
  awardedOn: '2026-01-20T23:00:00Z',
  endingOn: '2026-01-21T02:00:00Z',
  allDay: false,
  expiryDate: '2026-02-01T00:00:00Z',
  noticeState: 'None',
  notes: 'Current version',
  createdOn: '2026-01-20T00:00:00Z',
  updatedOn: null,
};

describe('UserTrainingVersionsModal', () => {
  afterEach(() => vi.useRealTimers());

  it.each([
    ['America/Vancouver', '2026-12-01', '2026-12-02T06:59:59.999Z', '3:00 PM'],
    ['America/Edmonton', '2026-07-01', '2026-07-02T05:59:59.999Z', '4:00 PM'],
  ])(
    'uses %s dates for details, history, and status across the UTC date boundary',
    async (timeZone, date, expiryDate, startTime) => {
      vi.useFakeTimers({ toFake: ['Date'] });
      vi.setSystemTime(Date.parse(expiryDate!) - 3600000);
      const training = { ...sampleTraining, awardedOn: `${date}T22:00:00Z`, endingOn: `${date}T23:00:00Z`, expiryDate };
      const wrapper = mount(UserTrainingVersionsModal, {
        props: { training, trainings: [training, { ...training, id: 9 }], timeZone },
        global: {
          stubs: {
            UaModal: UaModalStub,
            UaDataTable: UaDataTableStub,
            VTabs: VTabsStub,
            VTab: true,
            VRow: VRowStub,
            VCol: VColStub,
          },
        },
      });
      expect(wrapper.text()).toContain(`${date} ${startTime}`);
      expect(wrapper.text()).toContain('Active');
      await wrapper.get('[data-test="show-history"]').trigger('click');
      expect(wrapper.get('.ua-data-table-stub').text()).toContain(date!);
      expect(wrapper.get('.ua-data-table-stub').text()).toContain('Active');
      vi.setSystemTime(Date.parse(expiryDate!) + 1);
      await wrapper.get('[data-test="show-details"]').trigger('click');
      expect(wrapper.text()).toContain('Historical');
    },
  );
  it.each([false, true])('shows details dates with allDay=%s', (allDay) => {
    const training = allDay
      ? { ...sampleTraining, allDay, awardedOn: '2026-07-20T00:00:00Z', endingOn: '2026-07-22T00:00:00Z' }
      : sampleTraining;
    const wrapper = mount(UserTrainingVersionsModal, {
      props: {
        training,
        trainings: [training],
        timeZone: 'America/Vancouver',
      },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaDataTable: UaDataTableStub,
          VTabs: VTabsStub,
          VTab: true,
          VRow: VRowStub,
          VCol: VColStub,
        },
      },
    });

    expect(wrapper.text()).toContain('CPR');
    expect(wrapper.text()).toContain('Medical');
    expect(wrapper.text()).toContain('Current version');
    expect(wrapper.text()).toContain(allDay ? '2026-07-20' : '2026-01-20 3:00 PM');
    expect(wrapper.text()).toContain(allDay ? '2026-07-22' : '2026-01-20 6:00 PM');
    expect(wrapper.text()).toContain('2026-01-31');
    if (allDay) expect(wrapper.text()).not.toContain('2026-07-19');
  });

  it.each([false, true])('shows history dates with allDay=%s', async (allDay) => {
    const priorVersion = {
      ...sampleTraining,
      allDay,
      id: 9,
      version: 1,
      notes: 'Prior version',
      ...(allDay ? { awardedOn: '2026-07-20T00:00:00Z', endingOn: '2026-07-22T00:00:00Z' } : {}),
    };
    const wrapper = mount(UserTrainingVersionsModal, {
      props: {
        training: sampleTraining,
        trainings: [sampleTraining, priorVersion],
        timeZone: 'America/Vancouver',
      },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaDataTable: UaDataTableStub,
          VTabs: VTabsStub,
          VTab: true,
          VRow: VRowStub,
          VCol: VColStub,
        },
      },
    });

    await wrapper.get('[data-test="show-history"]').trigger('click');

    expect(wrapper.find('.ua-data-table-stub').exists()).toBe(true);
    expect(wrapper.find('.ua-data-table-stub').text()).toContain('rows: 1');
    expect(wrapper.find('.ua-data-table-stub').text()).toContain(allDay ? '2026-07-20' : '2026-01-20 3:00 PM');
    expect(wrapper.find('.ua-data-table-stub').text()).toContain(allDay ? '2026-07-22' : '2026-01-20 6:00 PM');
    if (allDay) expect(wrapper.find('.ua-data-table-stub').text()).not.toContain('2026-07-19');
  });
});
