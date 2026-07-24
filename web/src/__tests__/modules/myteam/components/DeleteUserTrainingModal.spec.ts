import DeleteUserTrainingModal from '@/modules/myteam/components/DeleteUserTrainingModal.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { deleteApiTrainingUserTrainingsIdMock } = vi.hoisted(() => ({
  deleteApiTrainingUserTrainingsIdMock: vi.fn(),
}));

vi.mock('@/api-access/generated/user-training/user-training', () => ({
  deleteApiTrainingUserTrainingsId: deleteApiTrainingUserTrainingsIdMock,
}));

const UaModalStub = defineComponent({
  name: 'UaModal',
  template:
    '<div><slot name="alerts" /><slot /><div><slot name="actions" /></div><button data-test="close" @click="$emit(\'close\')">close</button></div>',
  emits: ['close'],
});

const UaBtnStub = defineComponent({
  name: 'UaBtn',
  template: '<button @click="$emit(\'click\')"><slot /></button>',
  emits: ['click'],
});

const UaAlertStub = defineComponent({
  name: 'UaAlert',
  template: '<div><slot /></div>',
});

describe('DeleteUserTrainingModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('deletes record and emits deleted and close', async () => {
    deleteApiTrainingUserTrainingsIdMock.mockResolvedValue({
      error: ref(null),
    });

    const wrapper = mount(DeleteUserTrainingModal, {
      props: {
        training: {
          id: 42,
          userId: 'user-1',
          trainingId: 5,
          trainingCode: 'FIRST-AID',
          trainingCategoryName: 'Category',
          awardedOn: '2026-01-15T00:00:00Z',
          expiryDate: null,
          noticeState: 'None',
          notes: null,
          createdOn: '2026-01-15T00:00:00Z',
          updatedOn: null,
        },
      },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaBtn: UaBtnStub,
          UaAlert: UaAlertStub,
        },
      },
    });

    const deleteButton = wrapper.findAll('button').find((button) => button.text() === 'Delete');
    expect(deleteButton).toBeDefined();

    await deleteButton!.trigger('click');
    await flushPromises();

    expect(deleteApiTrainingUserTrainingsIdMock).toHaveBeenCalledWith(42);
    expect(wrapper.emitted('deleted')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('shows api error and does not emit deleted when delete fails', async () => {
    deleteApiTrainingUserTrainingsIdMock.mockResolvedValue({
      error: ref({ message: 'Delete failed from API' }),
    });

    const wrapper = mount(DeleteUserTrainingModal, {
      props: {
        training: {
          id: 42,
          userId: 'user-1',
          trainingId: 5,
          trainingCode: 'FIRST-AID',
          trainingCategoryName: 'Category',
          awardedOn: '2026-01-15T00:00:00Z',
          expiryDate: null,
          noticeState: 'None',
          notes: null,
          createdOn: '2026-01-15T00:00:00Z',
          updatedOn: null,
        },
      },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaBtn: UaBtnStub,
          UaAlert: UaAlertStub,
        },
      },
    });

    const deleteButton = wrapper.findAll('button').find((button) => button.text() === 'Delete');
    await deleteButton!.trigger('click');
    await flushPromises();

    expect(wrapper.text()).toContain('Delete failed from API');
    expect(wrapper.emitted('deleted')).toBeFalsy();
  });
});
