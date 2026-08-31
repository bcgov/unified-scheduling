import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import AuditFilters from '@/modules/audit/components/AuditFilters.vue';
import { createTestApp } from '../../../helpers/createTestApp';

const baseProps = {
  entityTypeOptions: [{ code: 'Shift', description: 'Shift' }],
  changedFieldOptions: [],
  actorOptions: [],
  entityType: null,
  entityPk: null,
  changedFields: [],
  actorUserId: null,
  action: null,
  fromDate: null,
  toDate: null,
};

describe('AuditFilters', () => {
  it('renders a labeled Entity ID filter and emits updates onP input', async () => {
    const app = await createTestApp();

    const wrapper = mount(AuditFilters, {
      props: { ...baseProps, canApply: false },
      global: { plugins: app.mountPlugins },
    });

    const label = wrapper.find('label[for="audit-filter-entity-id"]');
    expect(label.exists()).toBe(true);
    expect(label.text()).toBe('Entity ID');

    await wrapper.find('input#audit-filter-entity-id').setValue('12345');

    expect(wrapper.emitted('update:entityPk')?.[0]).toEqual(['12345']);
  });

  it('disables the search button until required filters are set', async () => {
    const app = await createTestApp();

    const wrapper = mount(AuditFilters, {
      props: { ...baseProps, canApply: false },
      global: { plugins: app.mountPlugins },
    });

    const buttons = wrapper.findAll('button');
    const searchButton = buttons.find((btn) => btn.text().includes('Search'));
    expect(searchButton?.attributes('disabled')).toBeDefined();

    await wrapper.setProps({ canApply: true });
    expect(searchButton?.attributes('disabled')).toBeUndefined();
  });

  it('emits apply and clear events', async () => {
    const app = await createTestApp();

    const wrapper = mount(AuditFilters, {
      props: { ...baseProps, canApply: true },
      global: { plugins: app.mountPlugins },
    });

    const buttons = wrapper.findAll('button');
    await buttons.find((btn) => btn.text().includes('Search'))?.trigger('click');
    await buttons.find((btn) => btn.text().includes('Clear'))?.trigger('click');

    expect(wrapper.emitted('apply')).toBeTruthy();
    expect(wrapper.emitted('clear')).toBeTruthy();
  });
});
