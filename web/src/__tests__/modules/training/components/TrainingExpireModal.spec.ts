import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import TrainingExpireModal from '@/modules/training/components/TrainingExpireModal.vue';
import { createTestApp } from '../../../helpers/createTestApp';
import type { TrainingLookupResponse } from '@/api-access/generated/models';

const training: TrainingLookupResponse = {
  id: 9,
  code: 'CPR',
  description: 'CPR',
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

describe('TrainingExpireModal', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('renders expire mode copy and emits confirm', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingExpireModal, {
      props: {
        training,
        mode: 'expire',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const content = document.body.textContent ?? '';
    expect(content).toContain('Expire Training');
    expect(content).toContain('Are you sure you want to expire');

    const confirmButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Expire'),
    );

    expect(confirmButton).toBeDefined();
    confirmButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(wrapper.emitted('confirm')).toBeTruthy();

    wrapper.unmount();
  });

  it('renders unexpire mode copy and emits confirm', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingExpireModal, {
      props: {
        training: {
          ...training,
          expiryDate: '2025-01-01T00:00:00Z',
        },
        mode: 'unexpire',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const content = document.body.textContent ?? '';
    expect(content).toContain('Unexpire Training');
    expect(content).toContain('Are you sure you want to unexpire');

    const confirmButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Unexpire'),
    );

    expect(confirmButton).toBeDefined();
    confirmButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(wrapper.emitted('confirm')).toBeTruthy();

    wrapper.unmount();
  });
});
