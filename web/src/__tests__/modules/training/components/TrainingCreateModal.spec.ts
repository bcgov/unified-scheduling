import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { ref } from 'vue';
import TrainingCreateModal from '@/modules/training/components/TrainingCreateModal.vue';
import { createTestApp } from '../../../helpers/createTestApp';

const { postApiTrainingsMock } = vi.hoisted(() => ({
  postApiTrainingsMock: vi.fn(),
}));

vi.mock('@/api-access/training', () => ({
  postApiTrainings: postApiTrainingsMock,
}));

describe('TrainingCreateModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    postApiTrainingsMock.mockResolvedValue({ data: ref(null), error: ref(null) });
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('renders create training form', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingCreateModal, {
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const content = document.body.textContent ?? '';
    expect(content).toContain('Create Training');
    expect(content).toContain('Training');
    expect(content).toContain('Description');

    wrapper.unmount();
  });

  it('shows validation errors when required values are missing', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingCreateModal, {
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const createButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Create Training'),
    );

    expect(createButton).toBeDefined();

    (createButton as HTMLButtonElement).click();
    await flushPromises();

    const content = document.body.textContent ?? '';
    expect(content).toContain('Required');
    expect(postApiTrainingsMock).not.toHaveBeenCalled();

    wrapper.unmount();
  });

  it('submits valid data and emits created + close', async () => {
    const app = await createTestApp();

    postApiTrainingsMock.mockResolvedValue({
      data: ref({
        id: 1,
        code: 'FIRE',
        description: 'Firearms Qualification',
        effectiveDate: '2026-01-01T00:00:00Z',
        expiryDate: null,
        mandatory: false,
        validityDays: 365,
        advanceNoticeDays: 30,
        rotating: false,
        trainingCategoryId: null,
        trainingCategoryName: null,
        order: 0,
        createdOn: '2026-01-01T00:00:00Z',
        updatedOn: null,
      }),
      error: ref(null),
    });

    const wrapper = mount(TrainingCreateModal, {
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const codeField = document.querySelector('#create-training-code') as HTMLInputElement;
    const descriptionField = document.querySelector('#create-training-description') as HTMLInputElement;

    codeField.value = 'FIRE';
    codeField.dispatchEvent(new Event('input', { bubbles: true }));
    descriptionField.value = 'Firearms Qualification';
    descriptionField.dispatchEvent(new Event('input', { bubbles: true }));

    await flushPromises();

    const createButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Create Training'),
    );

    (createButton as HTMLButtonElement).click();
    await flushPromises();

    expect(postApiTrainingsMock).toHaveBeenCalledWith({
      code: 'FIRE',
      description: 'Firearms Qualification',
      mandatory: false,
      validityDays: null,
      advanceNoticeDays: null,
      rotating: false,
      trainingCategoryId: null,
      order: 0,
    });

    expect(wrapper.emitted('created')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });
});
