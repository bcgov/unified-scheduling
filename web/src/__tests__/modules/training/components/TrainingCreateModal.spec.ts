import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { ref } from 'vue';
import TrainingCreateModal from '@/modules/training/components/TrainingCreateModal.vue';
import { createTestApp } from '../../../helpers/createTestApp';

const { postApiLookupTrainingsMock } = vi.hoisted(() => ({
  postApiLookupTrainingsMock: vi.fn(),
}));

vi.mock('@/api-access/generated/training/training', () => ({
  postApiLookupTrainings: postApiLookupTrainingsMock,
}));

vi.mock('@/modules/training/trainingProfileApi', () => ({
  useTrainingProfileLookup: () =>
    ({
      data: ref([
        { id: 1, code: 'GEN', name: 'General Duty' },
        { id: 2, code: 'SUP', name: 'Supervisor' },
      ]),
    }) as const,
}));

describe('TrainingCreateModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    postApiLookupTrainingsMock.mockResolvedValue({ data: ref(null), error: ref(null) });
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
    expect(postApiLookupTrainingsMock).not.toHaveBeenCalled();

    wrapper.unmount();
  });

  it('sends mandatory training profile ids when mandatory is true', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingCreateModal, {
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        code: string;
        description: string;
        mandatory: boolean;
        mandatoryTrainingProfileIds: number[];
        validityDayCode: string;
        advanceNoticeDays: string;
        rotating: boolean;
      };
    };

    vm.formData.code = 'FIRE';
    vm.formData.description = 'Firearms';
    vm.formData.mandatory = true;
    vm.formData.mandatoryTrainingProfileIds = [1, 2];

    await flushPromises();

    const createButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Create Training'),
    );

    (createButton as HTMLButtonElement).click();
    await flushPromises();

    expect(postApiLookupTrainingsMock).toHaveBeenCalledTimes(1);
    expect(postApiLookupTrainingsMock).toHaveBeenCalledWith(
      expect.objectContaining({
        code: 'FIRE',
        description: 'Firearms',
        mandatory: true,
        mandatoryTrainingProfileIds: [1, 2],
      }),
    );

    wrapper.unmount();
  });

  it('forces mandatory training profile ids to empty when mandatory is false', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingCreateModal, {
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        code: string;
        description: string;
        mandatory: boolean;
        mandatoryTrainingProfileIds: number[];
      };
    };

    vm.formData.code = 'CPR';
    vm.formData.description = 'First Aid';
    vm.formData.mandatory = false;
    vm.formData.mandatoryTrainingProfileIds = [1, 2];

    await flushPromises();

    const createButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Create Training'),
    );

    (createButton as HTMLButtonElement).click();
    await flushPromises();

    expect(postApiLookupTrainingsMock).toHaveBeenCalledTimes(1);
    expect(postApiLookupTrainingsMock).toHaveBeenCalledWith(
      expect.objectContaining({
        code: 'CPR',
        description: 'First Aid',
        mandatory: false,
        mandatoryTrainingProfileIds: [],
      }),
    );

    wrapper.unmount();
  });

  it('disables mandatory profiles selector when mandatory is false', async () => {
    const app = await createTestApp();

    const wrapper = mount(TrainingCreateModal, {
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const select = document.querySelector('#create-training-mandatory-profiles') as HTMLInputElement;
    expect(select).toBeTruthy();
    expect(select.disabled).toBe(true);

    wrapper.unmount();
  });
});
