import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import TrainingTable from '@/modules/training/components/TrainingTable.vue';
import { createTestApp } from '../../../helpers/createTestApp';
import type { TrainingResponse } from '@/api-access/training';

const { useDraggableMock } = vi.hoisted(() => ({
  useDraggableMock: vi.fn(),
}));

vi.mock('vue-draggable-plus', () => ({
  useDraggable: useDraggableMock,
}));

describe('TrainingTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useDraggableMock.mockReturnValue({ destroy: vi.fn() });
  });

  it('renders placeholder when there are no items and not loading', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingTable, {
      props: {
        items: [],
        loading: false,
        canEdit: false,
      },
      global: { plugins: app.mountPlugins },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('No trainings');
    expect(wrapper.text()).toContain('There are no training types to display yet.');
  });

  it('emits edit when edit button is clicked', async () => {
    const app = await createTestApp();

    const training: TrainingResponse = {
      id: 1,
      code: 'FIRE',
      description: 'Firearms Qualification',
      mandatory: true,
      validityDays: 365,
      advanceNoticeDays: 30,
      rotating: false,
      trainingCategoryId: null,
      trainingCategoryName: null,
      order: 0,
      createdOn: '2026-01-01T00:00:00Z',
      updatedOn: null,
    };

    const wrapper = mount(TrainingTable, {
      props: {
        items: [training],
        loading: false,
        canEdit: true,
      },
      global: { plugins: app.mountPlugins },
    });

    await flushPromises();

    const editButton = wrapper.find('button[aria-label="Edit training"]');
    expect(editButton.exists()).toBe(true);

    await editButton.trigger('click');

    expect(wrapper.emitted('edit')).toBeTruthy();
    expect(wrapper.emitted('edit')?.[0]).toEqual([training]);
  });

  it('formats nullable fields as em dash', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingTable, {
      props: {
        items: [
          {
            id: 1,
            code: 'CPR',
            description: 'First Aid',
            mandatory: false,
            validityDays: null,
            advanceNoticeDays: null,
            rotating: false,
            trainingCategoryId: null,
            trainingCategoryName: null,
            order: 0,
            createdOn: '2026-01-01T00:00:00Z',
            updatedOn: null,
          },
        ],
        loading: false,
        canEdit: false,
      },
      global: { plugins: app.mountPlugins },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('—');
  });
});
