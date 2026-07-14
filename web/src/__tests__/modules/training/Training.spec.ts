import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { ref } from 'vue';
import type { Permissions } from '@/api-access/generated/models';
import type { TrainingResponse } from '@/api-access/training';
import { createTestApp } from '../../helpers/createTestApp';
import Training from '@/modules/training/Training.vue';

const { getApiTrainingsMock, patchApiTrainingsIdOrderMock } = vi.hoisted(() => ({
  getApiTrainingsMock: vi.fn(),
  patchApiTrainingsIdOrderMock: vi.fn(),
}));

vi.mock('@/api-access/training', () => ({
  getApiTrainings: getApiTrainingsMock,
  patchApiTrainingsIdOrder: patchApiTrainingsIdOrderMock,
}));

describe('Training view', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    getApiTrainingsMock.mockReturnValue({
      data: ref<TrainingResponse[]>([]),
      error: ref<Error | null>(null),
      isFetching: ref(false),
      execute: vi.fn().mockResolvedValue(undefined),
    });

    patchApiTrainingsIdOrderMock.mockResolvedValue({ error: ref(null) });
  });

  it('shows permission placeholder when user lacks TrainingsView', async () => {
    const app = await createTestApp({ permissions: [] });

    const wrapper = mount(Training, {
      global: { plugins: app.mountPlugins },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('You do not have permission to view trainings.');
    expect(getApiTrainingsMock).toHaveBeenCalledWith({
      options: {
        immediate: false,
      },
    });
  });

  it('renders training table and add button when user has permissions', async () => {
    const app = await createTestApp({
      permissions: ['TrainingsView', 'TrainingsCreate'] as unknown as Permissions[],
    });

    getApiTrainingsMock.mockReturnValue({
      data: ref<TrainingResponse[]>([
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

    getApiTrainingsMock.mockReturnValue({
      data: ref<TrainingResponse[]>([]),
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
});
