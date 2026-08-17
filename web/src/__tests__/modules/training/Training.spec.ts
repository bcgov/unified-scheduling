import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent, ref } from 'vue';
import type { Permissions } from '@/api-access/generated/models';
import type { TrainingLookupResponse } from '@/api-access/generated/models';
import { createTestApp } from '../../helpers/createTestApp';
import Training from '@/modules/training/Training.vue';

const { useTrainingLookupMock, expireTrainingLookupMock, unexpireTrainingLookupMock, patchApiTrainingsIdOrderMock } =
  vi.hoisted(() => ({
    useTrainingLookupMock: vi.fn(),
    expireTrainingLookupMock: vi.fn(),
    unexpireTrainingLookupMock: vi.fn(),
    patchApiTrainingsIdOrderMock: vi.fn(),
  }));

vi.mock('@/api-access/generated/training/training', () => ({
  patchApiLookupTrainingsIdOrder: patchApiTrainingsIdOrderMock,
}));

vi.mock('@/modules/training/trainingLookupApi', () => ({
  useTrainingLookup: useTrainingLookupMock,
  expireTrainingLookup: expireTrainingLookupMock,
  unexpireTrainingLookup: unexpireTrainingLookupMock,
}));

describe('Training view', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    useTrainingLookupMock.mockReturnValue({
      data: ref<TrainingLookupResponse[]>([]),
      error: ref<Error | null>(null),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
    });

    patchApiTrainingsIdOrderMock.mockResolvedValue({ error: ref(null) });
    expireTrainingLookupMock.mockResolvedValue({ error: ref(null) });
    unexpireTrainingLookupMock.mockResolvedValue({ error: ref(null) });
  });

  it('shows permission placeholder when user lacks TrainingsView', async () => {
    const app = await createTestApp({ permissions: [] });

    const wrapper = mount(Training, {
      global: { plugins: app.mountPlugins },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('You do not have permission to view trainings.');
    expect(useTrainingLookupMock).toHaveBeenCalled();
  });

  it('renders training table and add button when user has permissions', async () => {
    const app = await createTestApp({
      permissions: ['TrainingsView', 'TrainingsCreate'] as unknown as Permissions[],
    });

    useTrainingLookupMock.mockReturnValue({
      data: ref<TrainingLookupResponse[]>([
        {
          id: 1,
          code: 'FIRE',
          description: 'Firearms Qualification',
          effectiveDate: '2026-01-01T00:00:00Z',
          expiryDate: null,
          mandatory: true,
          validityDays: 365,
          advanceNoticeDays: 30,
          rotating: false,
          trainingCategoryId: null,
          trainingCategoryName: null,
          order: 0,
          createdOn: '2026-01-01T00:00:00Z',
          updatedOn: null,
        },
      ]),
      error: ref<Error | null>(null),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
    });

    const wrapper = mount(Training, {
      global: {
        plugins: app.mountPlugins,
        stubs: {
          TrainingTable: { template: '<div data-test="training-table" />' },
          TrainingCreateModal: { template: '<div data-test="create-modal" />' },
          TrainingEditModal: { template: '<div data-test="edit-modal" />' },
        },
      },
    });

    await flushPromises();

    expect(wrapper.find('[data-test="training-table"]').exists()).toBe(true);
    expect(wrapper.text()).toContain('Add Training');

    const addButton = wrapper.findAll('button').find((button) => button.text().includes('Add Training'));

    expect(addButton).toBeDefined();

    await addButton!.trigger('click');
    await flushPromises();

    expect(wrapper.find('[data-test="create-modal"]').exists()).toBe(true);
  });

  it('renders error alert when trainings query fails', async () => {
    const app = await createTestApp({ permissions: ['TrainingsView'] as unknown as Permissions[] });

    useTrainingLookupMock.mockReturnValue({
      data: ref<TrainingLookupResponse[]>([]),
      error: ref<Error | null>(new Error('Unable to load training records')),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
    });

    const wrapper = mount(Training, {
      global: {
        plugins: app.mountPlugins,
        stubs: {
          TrainingTable: { template: '<div data-test="training-table" />' },
          TrainingCreateModal: { template: '<div data-test="create-modal" />' },
          TrainingEditModal: { template: '<div data-test="edit-modal" />' },
        },
      },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('Failed to load trainings: Unable to load training records');
  });

  it('confirms and calls expire endpoint when expire is selected', async () => {
    const app = await createTestApp({ permissions: ['TrainingsView', 'TrainingsEdit'] as unknown as Permissions[] });

    const TrainingTableStub = defineComponent({
      emits: ['expire'],
      template: '<button data-test="trigger-expire" @click="$emit(\'expire\', { id: 7 })">expire</button>',
    });

    const TrainingExpireModalStub = defineComponent({
      emits: ['confirm', 'close'],
      template: '<button data-test="confirm-expire" @click="$emit(\'confirm\')">confirm</button>',
    });

    const wrapper = mount(Training, {
      global: {
        plugins: app.mountPlugins,
        stubs: {
          TrainingTable: TrainingTableStub,
          TrainingCreateModal: { template: '<div />' },
          TrainingEditModal: { template: '<div />' },
          TrainingExpireModal: TrainingExpireModalStub,
        },
      },
    });

    await flushPromises();

    await wrapper.find('[data-test="trigger-expire"]').trigger('click');
    await flushPromises();

    await wrapper.find('[data-test="confirm-expire"]').trigger('click');
    await flushPromises();

    expect(expireTrainingLookupMock).toHaveBeenCalledWith(7);
    expect(unexpireTrainingLookupMock).not.toHaveBeenCalled();
  });

  it('confirms and calls unexpire endpoint when unexpire is selected', async () => {
    const app = await createTestApp({ permissions: ['TrainingsView', 'TrainingsEdit'] as unknown as Permissions[] });

    const TrainingTableStub = defineComponent({
      emits: ['unexpire'],
      template:
        '<button data-test="trigger-unexpire" @click="$emit(\'unexpire\', { id: 11, expiryDate: \'2024-01-01T00:00:00Z\' })">unexpire</button>',
    });

    const TrainingExpireModalStub = defineComponent({
      emits: ['confirm', 'close'],
      template: '<button data-test="confirm-unexpire" @click="$emit(\'confirm\')">confirm</button>',
    });

    const wrapper = mount(Training, {
      global: {
        plugins: app.mountPlugins,
        stubs: {
          TrainingTable: TrainingTableStub,
          TrainingCreateModal: { template: '<div />' },
          TrainingEditModal: { template: '<div />' },
          TrainingExpireModal: TrainingExpireModalStub,
        },
      },
    });

    await flushPromises();

    await wrapper.find('[data-test="trigger-unexpire"]').trigger('click');
    await flushPromises();

    await wrapper.find('[data-test="confirm-unexpire"]').trigger('click');
    await flushPromises();

    expect(unexpireTrainingLookupMock).toHaveBeenCalledWith(11);
    expect(expireTrainingLookupMock).not.toHaveBeenCalledWith(11);
  });
});
