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

  it('formats mandatory scope for non-mandatory, scoped mandatory, and all-users mandatory', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingTable, {
      props: {
        items: [
          {
            id: 11,
            code: 'NONMAND',
            description: 'Not mandatory',
            effectiveDate: '2026-01-01T00:00:00Z',
            expiryDate: null,
            mandatory: false,
            mandatoryTrainingProfiles: [{ id: 1, code: 'A', name: 'Profile A' }],
            validityDays: null,
            advanceNoticeDays: null,
            rotating: false,
            trainingCategoryId: null,
            trainingCategoryName: null,
            order: 0,
            createdOn: '2026-01-01T00:00:00Z',
            updatedOn: null,
          },
          {
            id: 12,
            code: 'SCOPED',
            description: 'Scoped mandatory',
            effectiveDate: '2026-01-01T00:00:00Z',
            expiryDate: null,
            mandatory: true,
            mandatoryTrainingProfiles: [
              { id: 2, code: 'CIV', name: 'Civil Team' },
              { id: 3, code: 'OPS', name: '  ' },
            ],
            validityDays: null,
            advanceNoticeDays: null,
            rotating: false,
            trainingCategoryId: null,
            trainingCategoryName: null,
            order: 1,
            createdOn: '2026-01-01T00:00:00Z',
            updatedOn: null,
          },
          {
            id: 13,
            code: 'GLOBAL',
            description: 'Global mandatory',
            effectiveDate: '2026-01-01T00:00:00Z',
            expiryDate: null,
            mandatory: true,
            mandatoryTrainingProfiles: [],
            validityDays: null,
            advanceNoticeDays: null,
            rotating: false,
            trainingCategoryId: null,
            trainingCategoryName: null,
            order: 2,
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

    const text = wrapper.text();
    expect(text).toContain('—');
    expect(text).toContain('Civil Team, OPS');
    expect(text).toContain('All users');
  });

  it('sets mandatory scope title to full value for long scoped lists', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingTable, {
      props: {
        items: [
          {
            id: 21,
            code: 'LONGSCOPE',
            description: 'Long scope',
            effectiveDate: '2026-01-01T00:00:00Z',
            expiryDate: null,
            mandatory: true,
            mandatoryTrainingProfiles: [
              { id: 1, code: 'A1', name: 'Profile One' },
              { id: 2, code: 'A2', name: 'Profile Two' },
              { id: 3, code: 'A3', name: 'Profile Three' },
              { id: 4, code: 'A4', name: 'Profile Four' },
            ],
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

    const scopeCell = wrapper.find('.mandatory-scope-cell');
    expect(scopeCell.exists()).toBe(true);
    expect(scopeCell.attributes('title')).toBe('Profile One, Profile Two, Profile Three, Profile Four');
  });
});
