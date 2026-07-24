import UserTrainingModal from '@/modules/myteam/components/UserTrainingModal.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { postApiTrainingUserTrainingsMock, putApiTrainingUserTrainingsIdMock } = vi.hoisted(() => ({
  postApiTrainingUserTrainingsMock: vi.fn(),
  putApiTrainingUserTrainingsIdMock: vi.fn(),
}));

vi.mock('@/api-access/generated/user-training/user-training', () => ({
  postApiTrainingUserTrainings: postApiTrainingUserTrainingsMock,
  putApiTrainingUserTrainingsId: putApiTrainingUserTrainingsIdMock,
}));

const UaModalStub = defineComponent({
  name: 'UaModal',
  emits: ['close'],
  template:
    '<div><slot name="alerts" /><slot /><div><slot name="actions" /></div><button data-test="close" @click="$emit(\'close\')">close</button></div>',
});

const UaBtnStub = defineComponent({
  name: 'UaBtn',
  emits: ['click'],
  template: '<button @click="$emit(\'click\')"><slot /></button>',
});

const UaAlertStub = defineComponent({
  name: 'UaAlert',
  template: '<div><slot /></div>',
});

describe('UserTrainingModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    postApiTrainingUserTrainingsMock.mockResolvedValue({ error: ref(null) });
    putApiTrainingUserTrainingsIdMock.mockResolvedValue({ error: ref(null) });
  });

  it('saves in edit mode with put and emits saved + close', async () => {
    const wrapper = mount(UserTrainingModal, {
      props: {
        userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
        trainingOptions: [],
        training: {
          id: 123,
          userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
          trainingId: 10,
          trainingCode: 'CPR',
          trainingCategoryName: 'Medical',
          awardedOn: '2026-01-20T00:00:00Z',
          expiryDate: '2026-12-31T00:00:00Z',
          noticeState: 'None',
          notes: '  keep me trimmed  ',
          createdOn: '2026-01-20T00:00:00Z',
          updatedOn: null,
        },
      },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaBtn: UaBtnStub,
          UaAlert: UaAlertStub,
          UaFormGrid: true,
          UaTextField: true,
          UaTextarea: true,
          UaSelect: true,
          VCheckbox: true,
        },
      },
    });

    const saveButton = wrapper.findAll('button').find((button) => button.text() === 'Save');
    expect(saveButton).toBeDefined();

    await saveButton!.trigger('click');
    await flushPromises();

    expect(putApiTrainingUserTrainingsIdMock).toHaveBeenCalledTimes(1);
    expect(putApiTrainingUserTrainingsIdMock).toHaveBeenCalledWith(
      123,
      expect.objectContaining({
        userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
        trainingId: 10,
        notes: 'keep me trimmed',
      }),
    );
    expect(postApiTrainingUserTrainingsMock).not.toHaveBeenCalled();
    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('does not submit in add mode when required training is missing', async () => {
    const wrapper = mount(UserTrainingModal, {
      props: {
        userId: '95f91fd1-1111-2222-3333-9c0aeb4ca44b',
        trainingOptions: [
          {
            code: 10,
            description: 'CPR - Basic',
          },
        ],
        training: null,
      },
      global: {
        stubs: {
          UaModal: UaModalStub,
          UaBtn: UaBtnStub,
          UaAlert: UaAlertStub,
          UaFormGrid: true,
          UaTextField: true,
          UaTextarea: true,
          UaSelect: true,
          VCheckbox: true,
        },
      },
    });

    const saveButton = wrapper.findAll('button').find((button) => button.text() === 'Save');
    await saveButton!.trigger('click');
    await flushPromises();

    expect(postApiTrainingUserTrainingsMock).not.toHaveBeenCalled();
    expect(putApiTrainingUserTrainingsIdMock).not.toHaveBeenCalled();
    expect(wrapper.emitted('saved')).toBeFalsy();
  });
});
