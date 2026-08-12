import UserTrainingVersionsModal from '@/modules/myteam/components/UserTrainingVersionsModal.vue';
import { mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { describe, expect, it } from 'vitest';

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
  template: '<div class="ua-data-table-stub">rows: {{ items.length }}</div>',
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
  awardedOn: '2026-01-20T00:00:00Z',
  endingOn: '2026-01-20T00:00:00Z',
  expiryDate: '2026-02-01T00:00:00Z',
  noticeState: 'None',
  notes: 'Current version',
  createdOn: '2026-01-20T00:00:00Z',
  updatedOn: null,
};

describe('UserTrainingVersionsModal', () => {
  it('shows details tab by default', () => {
    const wrapper = mount(UserTrainingVersionsModal, {
      props: {
        training: sampleTraining,
        trainings: [sampleTraining],
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
  });

  it('shows history table when history tab is selected', async () => {
    const wrapper = mount(UserTrainingVersionsModal, {
      props: {
        training: sampleTraining,
        trainings: [
          sampleTraining,
          {
            ...sampleTraining,
            id: 9,
            version: 1,
            notes: 'Prior version',
          },
        ],
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
  });
});
