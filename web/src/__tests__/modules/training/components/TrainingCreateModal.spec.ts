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
});
