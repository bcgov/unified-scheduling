import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { mdiClose } from '@mdi/js';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import {
  Permissions,
  type AssignmentDefinitionRequest,
  type AssignmentDefinitionResponse,
} from '@/api-access/generated/models';
import { getGetApiSchedulingAssignmentDefinitionsIdMockHandler } from '@/api-access/generated/assignment-definition/assignment-definition.msw';
import { getGetApiStatsGroupsMockHandler } from '@/api-access/generated/stat-groups/stat-groups.msw';
import { getGetApiStatsCategoriesMockHandler } from '@/api-access/generated/stat-categories/stat-categories.msw';
import { getGetApiStatsSubCategoriesMockHandler } from '@/api-access/generated/sub-categories/sub-categories.msw';
import CalendarSchedulingAssignmentDefinitionCreateModal from '@/modules/scheduling/CalendarSchedulingAssignmentDefinitionCreateModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { useLocationsStore } from '@/stores/LocationsStore';

enableAutoUnmount(afterEach);

describe('Assignment template required fields and optional times', () => {
  let saved: AssignmentDefinitionResponse;
  let requests: AssignmentDefinitionRequest[];

  beforeEach(() => {
    saved = {
      id: 7,
      locationId: 12,
      name: 'Court Coverage',
      description: '',
      categoryId: 10,
      subCategoryId: 20,
      color: 'blue',
      defaultCapacity: 3,
      defaultStartTime: '09:00',
      defaultEndTime: '17:00',
      effectiveDateUtc: '2026-07-01T00:00:00Z',
    };
    requests = [];
    const saveRequest = async ({ request }: { request: Request }) => {
      const body = (await request.json()) as AssignmentDefinitionRequest;
      requests.push(body);
      saved = { ...saved, ...body };
      return HttpResponse.json(saved);
    };
    server.use(
      getGetApiSchedulingAssignmentDefinitionsIdMockHandler(() => saved),
      getGetApiStatsGroupsMockHandler([{ id: 3, name: 'Location Level' }]),
      getGetApiStatsCategoriesMockHandler([{ id: 10, groupId: 3, name: 'Court' }]),
      getGetApiStatsSubCategoriesMockHandler([{ id: 20, categoryId: 10, name: 'Registry' }]),
      http.put('*/api/scheduling/assignment-definitions/:id', saveRequest),
      http.post('*/api/scheduling/assignment-definitions', saveRequest),
    );
  });

  async function mountTemplate(mode: 'create' | 'edit' | 'view' = 'edit') {
    const app = await createTestApp({ loadConfig: false, permissions: Object.values(Permissions) });
    useLocationsStore(app.pinia).entities = [{ id: 12, name: 'Vancouver' }];
    const wrapper = mount(CalendarSchedulingAssignmentDefinitionCreateModal, {
      props: { mode, assignmentDefinitionId: mode === 'create' ? undefined : 7, currentLocationId: 12 },
      global: {
        plugins: app.mountPlugins,
        stubs: { UaModal: { template: '<section><slot name="alerts" /><slot /><slot name="actions" /></section>' } },
      },
    });
    await flushPromises();
    await vi.waitFor(() => {
      if (mode === 'view') {
        expect(wrapper.text()).toContain('Court Coverage');
        return;
      }

      // Wait for the rendered controls, not a prop that may precede their DOM update.
      for (const field of ['start', 'end']) {
        expect(
          wrapper.get(`input#assignment-definition-modal-default-${field}`).attributes('disabled'),
        ).toBeUndefined();
      }
      const group = wrapper
        .findAllComponents(UaSelect)
        .find((item) => item.find('input#assignment-definition-modal-group').exists())!;
      expect(group.props('items')).toEqual([{ code: 3, description: 'Location Level' }]);
      if (mode === 'edit') {
        expect((wrapper.get('input#assignment-definition-modal-name').element as HTMLInputElement).value).toBe(
          'Court Coverage',
        );
      }
    });
    return wrapper;
  }

  it('marks required fields including Group while leaving optional defaults unmarked', async () => {
    const wrapper = await mountTemplate();
    for (const id of ['name', 'location', 'group', 'category', 'subcategory', 'capacity', 'effective-date']) {
      expect(wrapper.get(`label[for="assignment-definition-modal-${id}"]`).text()).toContain('(required)');
      expect(wrapper.get(`input#assignment-definition-modal-${id}`).attributes('aria-required')).toBe('true');
    }
    for (const id of ['description', 'color', 'default-start', 'default-end', 'expiry-date']) {
      expect(wrapper.get(`label[for="assignment-definition-modal-${id}"]`).text()).not.toContain('(required)');
    }
  });

  it.each(['start', 'end', 'both'] as const)(
    'clears %s and persists unset values when the template is reopened',
    async (which) => {
      const wrapper = await mountTemplate();
      const fields = which === 'both' ? ['start', 'end'] : [which];
      for (const field of fields) {
        const select = wrapper
          .findAllComponents(UaSelect)
          .find((item) => item.find(`input#assignment-definition-modal-default-${field}`).exists())!;
        expect(select.props('clearable')).toBe(true);
        expect(select.props('persistentClear')).toBe(true);
        expect(select.get('.v-field__clearable').isVisible()).toBe(true);
        expect(select.getComponent({ name: 'VSelect' }).props('clearIcon')).toBe(mdiClose);
        await select.get('.v-field__clearable .v-icon').trigger('click');
        await flushPromises();
      }
      const save = wrapper.findAll('button').find((button) => button.text() === 'Save')!;
      await save.trigger('click');
      await vi.waitFor(() => expect(requests).toHaveLength(1));
      expect(requests[0]).toMatchObject({
        defaultStartTime: which === 'end' ? '09:00' : null,
        defaultEndTime: which === 'start' ? '17:00' : null,
      });
      wrapper.unmount();
      const reopened = await mountTemplate();
      for (const field of fields) {
        const select = reopened
          .findAllComponents(UaSelect)
          .find((item) => item.find(`input#assignment-definition-modal-default-${field}`).exists())!;
        expect(select.props('modelValue')).toBeNull();
      }
    },
  );

  it('preserves existing unset times when opening and saving without edits', async () => {
    saved.defaultStartTime = null;
    saved.defaultEndTime = null;
    const wrapper = await mountTemplate();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await vi.waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]).toMatchObject({ defaultStartTime: null, defaultEndTime: null });
  });

  it('accepts unset default times when creating a template', async () => {
    const wrapper = await mountTemplate('create');
    const select = (id: string) =>
      wrapper
        .findAllComponents(UaSelect)
        .find((item) => item.find(`input#assignment-definition-modal-${id}`).exists())!;
    await wrapper.get('input#assignment-definition-modal-name').setValue('New template');
    select('group').vm.$emit('update:modelValue', 3);
    await flushPromises();
    select('category').vm.$emit('update:modelValue', 10);
    await flushPromises();
    select('subcategory').vm.$emit('update:modelValue', 20);
    select('default-start').vm.$emit('update:modelValue', null);
    select('default-end').vm.$emit('update:modelValue', null);
    await flushPromises();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await vi.waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]).toMatchObject({ name: 'New template', defaultStartTime: null, defaultEndTime: null });
  });

  it('still rejects an end time before the start time when both are set', async () => {
    const wrapper = await mountTemplate();
    const end = wrapper
      .findAllComponents(UaSelect)
      .find((item) => item.find('input#assignment-definition-modal-default-end').exists())!;
    end.vm.$emit('update:modelValue', '08:00');
    await flushPromises();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await flushPromises();
    expect(end.getComponent({ name: 'VSelect' }).props('error')).toBe(true);
    expect(requests).toHaveLength(0);
  });

  it('displays unset times as None in the details view', async () => {
    saved.defaultStartTime = null;
    saved.defaultEndTime = null;
    const wrapper = await mountTemplate('view');
    for (const label of ['Default Start', 'Default End']) {
      const fieldLabel = wrapper.findAll('.shift-details-panel__label').find((item) => item.text() === label)!;
      expect(fieldLabel.element.nextElementSibling?.textContent).toBe('None');
    }
    expect(wrapper.find('.ua-required-indicator').exists()).toBe(false);
  });
});
