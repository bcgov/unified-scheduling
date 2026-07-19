import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useLocationsStore } from '@/stores/LocationsStore';

const UaModalStub = defineComponent({
  template: '<section><slot name="alerts" /><slot /><slot name="actions" /></section>',
});

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
});
