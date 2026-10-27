import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import AssignmentRow from '@/modules/stats/components/AssignmentRow.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import type { AssignmentData } from '@/modules/stats/types';

describe('AssignmentRow flat demographic subcategories', () => {
  it.each([
    { categoryId: 29, subCategoryId: 117, firstScmId: 449, name: 'Holding area/cellblock', metricIds: [33, 34] },
    { categoryId: 27, subCategoryId: 123, firstScmId: 461, name: 'Transports Air', metricIds: [36, 38, 40, 42] },
    { categoryId: 28, subCategoryId: 129, firstScmId: 485, name: 'Transports Ground', metricIds: [37, 39, 41, 42] },
  ])('selects a $name demographic and renders only its configured metrics', async (area) => {
    const app = await createTestApp({ loadConfig: false });
    const model: AssignmentData = {
      id: 'entry', groupId: 3, categoryId: null, subCategoryId: null, metricValues: {}, comment: '', performedAtLocationId: null,
    };
    const wrapper = mount(AssignmentRow, {
      props: {
        modelValue: model, fixedGroupId: 3, index: 0, errors: {},
        groups: [{ id: 3, name: 'Location Level' }],
        categories: [{ id: area.categoryId, groupId: 3, name: area.name }],
        subCategories: [
          { id: area.subCategoryId - 1, categoryId: area.categoryId, name: 'Hours or trips', displayOrder: 1 },
          { id: area.subCategoryId, categoryId: area.categoryId, name: 'Male - Provincial Adult', displayOrder: 2 },
        ],
        subCategoryMetrics: [
          { id: 407, subCategoryId: area.subCategoryId - 1, metricId: 14 },
          ...area.metricIds.map((metricId, index) => ({ id: area.firstScmId + index, subCategoryId: area.subCategoryId, metricId })),
        ],
        metrics: [
          { id: 14, name: 'Cell Block Hours', unitOfMeasure: 'hours' },
          ...area.metricIds.map(id => ({ id, name: `Metric ${id}`, unitOfMeasure: 'count' })),
        ],
      },
      global: { plugins: app.mountPlugins },
    });
    try {
      wrapper.findAllComponents(UaSelect).find(s => s.vm.$attrs.label === 'Select Work Area')!
        .vm.$emit('update:modelValue', area.categoryId);
      await wrapper.setProps({ modelValue: wrapper.emitted('update:modelValue')!.at(-1)![0] as AssignmentData });
      expect(wrapper.findAll('input[type="number"]')).toHaveLength(0);
      wrapper.findAllComponents(UaSelect).find(s => s.vm.$attrs.label === 'Select Subcategory')!
        .vm.$emit('update:modelValue', area.subCategoryId);
      await wrapper.setProps({ modelValue: wrapper.emitted('update:modelValue')!.at(-1)![0] as AssignmentData });
      const inputs = wrapper.findAll('input[type="number"]');
      expect(inputs).toHaveLength(area.metricIds.length);
      expect(wrapper.text()).not.toContain('Cell Block Hours');
      for (const metricId of area.metricIds) expect(wrapper.text()).toContain(`Metric ${metricId}`);

      await inputs[0]!.setValue('7');
      await flushPromises();
      const updated = wrapper.emitted('update:modelValue')!.at(-1)![0] as AssignmentData;
      expect(updated.subCategoryId).toBe(area.subCategoryId);
      expect(updated.metricValues).toEqual({ [area.firstScmId]: '7' });
    } finally {
      wrapper.unmount();
    }
  });
});
