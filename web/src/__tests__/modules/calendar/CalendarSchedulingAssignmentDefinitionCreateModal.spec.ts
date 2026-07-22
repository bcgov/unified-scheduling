import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useLocationsStore } from '@/stores/LocationsStore';

const UaModalStub = defineComponent({
  template: '<section><slot name="alerts" /><slot /><slot name="actions" /></section>',
});

function createFetchResult<T>(value: T, execute = vi.fn().mockResolvedValue(undefined)) {
  return {
    data: { value },
    error: { value: null },
    execute,
  };
}

describe('CalendarSchedulingAssignmentDefinitionCreateModal', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('renders assignment type details as read-only values in view mode', async () => {
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitionsId: vi.fn().mockReturnValue({
        data: {
          value: {
            id: 7,
            locationId: 12,
            name: 'Court Coverage',
            description: 'Courtroom assignment',
            assignmentCategoryTypeId: 10,
            assignmentSubCategoryTypeId: 20,
            color: 'blue',
            defaultStartTime: '09:00',
            defaultEndTime: '17:00',
            defaultCapacity: 3,
          },
        },
        error: { value: null },
        execute: vi.fn().mockResolvedValue(undefined),
      }),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
      putApiSchedulingAssignmentDefinitionsId: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/lookup/lookup', () => ({
      getApiLookupCodeType: vi.fn().mockImplementation((codeType: string) => ({
        data: {
          value:
            codeType === 'AssignmentCategoryTypes'
              ? [
                  {
                    parentCodeTypeId: 10,
                    description: 'Court',
                    childCodeTypeIds: [20],
                  },
                ]
              : [
                  {
                    parentCodeTypeId: 10,
                    childCodeTypeId: 20,
                    description: 'Registry',
                  },
                ],
        },
        error: { value: null },
        execute: vi.fn().mockResolvedValue(undefined),
      })),
    }));

    const { default: CalendarSchedulingAssignmentDefinitionCreateModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentDefinitionCreateModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.entities = [{ id: 12, name: 'Vancouver' }];

    const wrapper = mount(CalendarSchedulingAssignmentDefinitionCreateModal, {
      props: {
        assignmentDefinitionId: 7,
        mode: 'view',
      },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: UaModalStub,
        },
      },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('Court Coverage');
    expect(wrapper.text()).toContain('Courtroom assignment');
    expect(wrapper.text()).toContain('Vancouver');
    expect(wrapper.text()).toContain('Court');
    expect(wrapper.text()).toContain('Registry');
    expect(wrapper.text()).toContain('3');
    expect(wrapper.find('.shift-details-panel__color-sphere').exists()).toBe(true);
    expect(wrapper.find('.shift-details-panel__color-sphere').attributes('aria-label')).toBe('Blue');
    expect(wrapper.find('#assignment-definition-modal-name').exists()).toBe(false);

    wrapper.unmount();
  });

  it('preserves existing effective and expiry dates when editing without changes', async () => {
    const updateExecute = vi.fn().mockResolvedValue(undefined);
    const putApiSchedulingAssignmentDefinitionsId = vi
      .fn()
      .mockReturnValue(createFetchResult({ id: 7 }, updateExecute));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitionsId: vi.fn().mockReturnValue(
        createFetchResult({
          id: 7,
          locationId: 12,
          name: 'Court Coverage',
          description: 'Courtroom assignment',
          assignmentCategoryTypeId: 10,
          assignmentSubCategoryTypeId: 20,
          color: 'blue',
          defaultStartTime: '09:00',
          defaultEndTime: '17:00',
          defaultCapacity: 3,
          effectiveDateUtc: '2026-08-01T07:00:00Z',
          expiryDateUtc: '2026-09-01T07:00:00Z',
        }),
      ),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
      putApiSchedulingAssignmentDefinitionsId,
    }));
    vi.doMock('@/api-access/generated/lookup/lookup', () => ({
      getApiLookupCodeType: vi.fn().mockImplementation((codeType: string) =>
        createFetchResult(
          codeType === 'AssignmentCategoryTypes'
            ? [{ parentCodeTypeId: 10, description: 'Court', childCodeTypeIds: [20], effectiveDate: '2020-01-01T00:00:00Z' }]
            : [{ parentCodeTypeId: 10, childCodeTypeId: 20, description: 'Registry', effectiveDate: '2020-01-01T00:00:00Z' }],
        ),
      ),
    }));

    const { default: CalendarSchedulingAssignmentDefinitionCreateModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentDefinitionCreateModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.entities = [{ id: 12, name: 'Vancouver', timezone: 'America/Vancouver' }];

    const wrapper = mount(CalendarSchedulingAssignmentDefinitionCreateModal, {
      props: {
        assignmentDefinitionId: 7,
        mode: 'edit',
      },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: UaModalStub,
        },
      },
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { effectiveDateUtc?: string | null; expiryDateUtc?: string | null };
      handleSave: () => Promise<void>;
    };

    expect(vm.formData.effectiveDateUtc).toBe('2026-08-01');
    expect(vm.formData.expiryDateUtc).toBe('2026-09-01');

    await vm.handleSave();
    await flushPromises();

    expect(putApiSchedulingAssignmentDefinitionsId).toHaveBeenCalledWith(
      7,
      expect.objectContaining({
        effectiveDateUtc: '2026-08-01T07:00:00Z',
        expiryDateUtc: '2026-09-01T07:00:00Z',
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );

    wrapper.unmount();
  });

  it('defaults new assignment definitions to today with no expiry date', async () => {
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitionsId: vi.fn(),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
      putApiSchedulingAssignmentDefinitionsId: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/lookup/lookup', () => ({
      getApiLookupCodeType: vi.fn().mockReturnValue(createFetchResult([])),
    }));

    const { default: CalendarSchedulingAssignmentDefinitionCreateModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentDefinitionCreateModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.entities = [{ id: 12, name: 'Vancouver', timezone: 'America/Vancouver' }];
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAssignmentDefinitionCreateModal, {
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: UaModalStub,
        },
      },
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { effectiveDateUtc?: string | null; expiryDateUtc?: string | null };
    };

    expect(vm.formData.effectiveDateUtc).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(vm.formData.expiryDateUtc).toBeNull();

    wrapper.unmount();
  });

  it('filters category options by the assignment definition effective date while preserving the selected values', async () => {
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitionsId: vi.fn().mockReturnValue(
        createFetchResult({
          id: 7,
          locationId: 12,
          name: 'Future Coverage',
          assignmentCategoryTypeId: 10,
          assignmentSubCategoryTypeId: 20,
          defaultCapacity: 1,
          defaultStartTime: '09:00',
          defaultEndTime: '17:00',
          effectiveDateUtc: '2026-08-01T07:00:00Z',
          expiryDateUtc: null,
        }),
      ),
      postApiSchedulingAssignmentDefinitions: vi.fn(),
      putApiSchedulingAssignmentDefinitionsId: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/lookup/lookup', () => ({
      getApiLookupCodeType: vi.fn().mockImplementation((codeType: string) =>
        createFetchResult(
          codeType === 'AssignmentCategoryTypes'
            ? [
                {
                  parentCodeTypeId: 10,
                  description: 'Selected expired court',
                  childCodeTypeIds: [20],
                  effectiveDate: '2020-01-01T00:00:00Z',
                  expiryDate: '2026-07-01T00:00:00Z',
                },
                {
                  parentCodeTypeId: 11,
                  description: 'Future category',
                  childCodeTypeIds: [21],
                  effectiveDate: '2026-08-01T00:00:00Z',
                },
                {
                  parentCodeTypeId: 12,
                  description: 'Too future category',
                  childCodeTypeIds: [22],
                  effectiveDate: '2026-09-01T00:00:00Z',
                },
              ]
            : [
                {
                  parentCodeTypeId: 10,
                  childCodeTypeId: 20,
                  description: 'Selected expired registry',
                  effectiveDate: '2020-01-01T00:00:00Z',
                  expiryDate: '2026-07-01T00:00:00Z',
                },
                {
                  parentCodeTypeId: 11,
                  childCodeTypeId: 21,
                  description: 'Future registry',
                  effectiveDate: '2026-08-01T00:00:00Z',
                },
              ],
        ),
      ),
    }));

    const { default: CalendarSchedulingAssignmentDefinitionCreateModal } =
      await import('@/modules/scheduling/CalendarSchedulingAssignmentDefinitionCreateModal.vue');

    const app = await createTestApp({ loadConfig: false });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.entities = [{ id: 12, name: 'Vancouver', timezone: 'America/Vancouver' }];

    const wrapper = mount(CalendarSchedulingAssignmentDefinitionCreateModal, {
      props: {
        assignmentDefinitionId: 7,
        mode: 'edit',
      },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: UaModalStub,
        },
      },
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      assignmentCategoryOptions: Array<{ code: number; description: string }>;
      assignmentSubCategoryOptions: Array<{ code: number; description: string }>;
    };

    expect(vm.assignmentCategoryOptions.map((option) => option.code)).toEqual([11, 10]);
    expect(vm.assignmentSubCategoryOptions.map((option) => option.code)).toEqual([20]);

    wrapper.unmount();
  });
});
