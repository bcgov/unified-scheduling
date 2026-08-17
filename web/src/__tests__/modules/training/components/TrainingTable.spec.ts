import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import TrainingTable from '@/modules/training/components/TrainingTable.vue';
import { createTestApp } from '../../../helpers/createTestApp';
import type { TrainingLookupResponse } from '@/api-access/generated/models';

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

    expect(wrapper.text()).toContain('No trainings found.');
  });

  it('emits edit when edit button is clicked', async () => {
    const app = await createTestApp();

    const training: TrainingLookupResponse = {
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

  it('emits expire when expire button is clicked for active trainings', async () => {
    const app = await createTestApp();

    const training: TrainingLookupResponse = {
      id: 2,
      code: 'CPR',
      description: 'CPR',
      effectiveDate: '2026-01-01T00:00:00Z',
      expiryDate: null,
      mandatory: false,
      validityDays: null,
      advanceNoticeDays: null,
      rotating: false,
      trainingCategoryId: null,
      trainingCategoryName: null,
      order: 1,
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

    const expireButton = wrapper.find('button[aria-label="Expire training"]');
    expect(expireButton.exists()).toBe(true);

    await expireButton.trigger('click');

    expect(wrapper.emitted('expire')).toBeTruthy();
    expect(wrapper.emitted('expire')?.[0]).toEqual([training]);
  });

  it('emits unexpire when unexpire button is clicked for expired trainings', async () => {
    const app = await createTestApp();

    const training: TrainingLookupResponse = {
      id: 3,
      code: 'OLD',
      description: 'Expired Training',
      effectiveDate: '2025-01-01T00:00:00Z',
      expiryDate: '2025-01-02T00:00:00Z',
      mandatory: false,
      validityDays: null,
      advanceNoticeDays: null,
      rotating: false,
      trainingCategoryId: null,
      trainingCategoryName: null,
      order: 2,
      createdOn: '2025-01-01T00:00:00Z',
      updatedOn: null,
    };

    const wrapper = mount(TrainingTable, {
      props: {
        items: [training],
        loading: false,
        canEdit: true,
        highlightExpiredRows: true,
      },
      global: { plugins: app.mountPlugins },
    });

    await flushPromises();

    const unexpireButton = wrapper.find('button[aria-label="Unexpire training"]');
    expect(unexpireButton.exists()).toBe(true);

    await unexpireButton.trigger('click');

    expect(wrapper.emitted('unexpire')).toBeTruthy();
    expect(wrapper.emitted('unexpire')?.[0]).toEqual([training]);
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
            effectiveDate: '2026-01-01T00:00:00Z',
            expiryDate: null,
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
