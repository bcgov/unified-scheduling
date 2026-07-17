import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { ref } from 'vue';
import TrainingEditModal from '@/modules/training/components/TrainingEditModal.vue';
import { createTestApp } from '../../../helpers/createTestApp';
import type { TrainingLookupResponse } from '@/api-access/generated/models';

const { putApiTrainingsIdMock } = vi.hoisted(() => ({
  putApiTrainingsIdMock: vi.fn(),
}));

vi.mock('@/api-access/generated/training/training', () => ({
  putApiLookupTrainingsId: putApiTrainingsIdMock,
}));

const training: TrainingLookupResponse = {
  id: 10,
  code: 'FIRE',
  description: 'Firearms Qualification',
  effectiveDate: '2026-01-01T00:00:00Z',
  expiryDate: null,
  mandatory: true,
  validityDays: 365,
  advanceNoticeDays: 30,
  rotating: false,
  trainingCategoryId: null,
  trainingCategoryName: 'Operational',
  createdOn: '2026-01-01T00:00:00Z',
  updatedOn: null,
};

describe('TrainingEditModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    putApiTrainingsIdMock.mockResolvedValue({ data: ref(training), error: ref(null) });
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('renders edit form and training values', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingEditModal, {
      props: { training },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const content = document.body.textContent ?? '';
    expect(content).toContain('Edit Training');
    expect(content).toContain('Operational');

    const codeField = document.querySelector('#training-code') as HTMLInputElement;
    expect(codeField.value).toBe('FIRE');

    wrapper.unmount();
  });

  it('submits updated data and emits updated + close', async () => {
    const app = await createTestApp();

    putApiTrainingsIdMock.mockResolvedValue({
      data: ref({ ...training, description: 'Updated qualification' }),
      error: ref(null),
    });

    const wrapper = mount(TrainingEditModal, {
      props: { training },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const descriptionField = document.querySelector('#training-description') as HTMLInputElement;
    descriptionField.value = 'Updated qualification';
    descriptionField.dispatchEvent(new Event('input', { bubbles: true }));

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save Changes'),
    );

    (saveButton as HTMLButtonElement).click();
    await flushPromises();

    expect(putApiTrainingsIdMock).toHaveBeenCalledWith(10, {
      code: 'FIRE',
      description: 'Updated qualification',
      mandatory: true,
      validityDays: 365,
      advanceNoticeDays: 30,
      rotating: false,
      trainingCategoryId: null,
    });

    expect(wrapper.emitted('updated')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('shows validation errors when required values are missing', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingEditModal, {
      props: { training },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const codeField = document.querySelector('#training-code') as HTMLInputElement;
    const descriptionField = document.querySelector('#training-description') as HTMLInputElement;

    codeField.value = '';
    codeField.dispatchEvent(new Event('input', { bubbles: true }));
    descriptionField.value = '';
    descriptionField.dispatchEvent(new Event('input', { bubbles: true }));

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save Changes'),
    );

    (saveButton as HTMLButtonElement).click();
    await flushPromises();

    const content = document.body.textContent ?? '';
    expect(content).toContain('Required');
    expect(putApiTrainingsIdMock).not.toHaveBeenCalled();

    wrapper.unmount();
  });
});
