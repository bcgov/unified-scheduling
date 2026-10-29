import { afterEach, describe, expect, it } from 'vitest';
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import UaRequiredIndicator from '@/shared/components/UaRequiredIndicator.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import UaSelect from '@/shared/components/UaSelect.vue';

enableAutoUnmount(afterEach);

describe('Required field controls', () => {
  it('provides screen-reader text while hiding the decorative asterisk', () => {
    const wrapper = mount(UaRequiredIndicator);
    expect(wrapper.get('.ua-required-indicator').attributes('aria-hidden')).toBe('true');
    expect(wrapper.get('.ua-required-indicator__text').text()).toBe('(required)');
    expect(wrapper.get('.ua-required-indicator__text').attributes('aria-hidden')).toBeUndefined();
  });

  it.each(['text', 'textarea'])('updates visible and accessible required status on %s controls', async (kind) => {
    const app = await createTestApp({ loadConfig: false });
    const options = {
      props: { id: 'required-field', label: 'Notes' },
      global: { plugins: app.mountPlugins },
    };
    const wrapper = kind === 'text' ? mount(UaTextField, options) : mount(UaTextarea, options);
    const control = () => wrapper.get('input, textarea');

    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
    expect(control().attributes('aria-required')).toBeUndefined();
    await wrapper.setProps({ required: true });
    expect(wrapper.get('label[for="required-field"]').text()).toContain('(required)');
    expect(control().attributes('aria-required')).toBe('true');
    // Required display must not introduce browser validation.
    expect(control().attributes('required')).toBeUndefined();
    await wrapper.setProps({ required: false });
    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
    expect(control().attributes('aria-required')).toBeUndefined();
  });

  it('keeps custom text-field labels and announces their required status', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(UaTextField, {
      props: { id: 'custom-label', label: '', required: true },
      slots: { label: 'Custom name' },
      global: { plugins: app.mountPlugins },
    });
    expect(wrapper.get('label').text()).toContain('Custom name');
    expect(wrapper.get('label').text()).toContain('(required)');
  });

  it('marks select labels only when required and preserves external required attributes', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(UaSelect, {
      props: { items: [], required: true },
      attrs: { label: 'Users' },
      global: { plugins: app.mountPlugins },
    });
    expect(wrapper.get('label').text()).toContain('(required)');
    expect(wrapper.get('input[role="combobox"]').attributes('aria-required')).toBe('true');
    await wrapper.setProps({ required: false });
    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
    expect(wrapper.get('input[role="combobox"]').attributes('aria-required')).toBeUndefined();
    const externallyRequired = mount(UaSelect, {
      props: { items: [] },
      attrs: { 'aria-required': 'true' },
      global: { plugins: app.mountPlugins },
    });
    expect(externallyRequired.get('input[role="combobox"]').attributes('aria-required')).toBe('true');
  });

  it('renders a persistent clear control and emits an unset value when clicked', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(UaSelect, {
      props: {
        items: [{ code: '09:00', description: '9:00 AM' }],
        modelValue: '09:00',
        clearable: true,
        persistentClear: true,
      },
      global: { plugins: app.mountPlugins },
    });
    expect(wrapper.find('.v-field--persistent-clear').exists()).toBe(true);
    expect(wrapper.get('.v-field__clearable').isVisible()).toBe(true);
    await wrapper.get('.v-field__clearable .v-icon').trigger('click');
    await flushPromises();
    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([null]);
  });

  it('does not expose clearing on ordinary or disabled selects', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(UaSelect, {
      props: { items: [{ code: '09:00', description: '9:00 AM' }], modelValue: '09:00' },
      global: { plugins: app.mountPlugins },
    });
    expect(wrapper.find('.v-field__clearable').exists()).toBe(false);
    const disabled = mount(UaSelect, {
      props: { items: [{ code: '09:00', description: '9:00 AM' }], modelValue: '09:00', clearable: true },
      attrs: { disabled: true },
      global: { plugins: app.mountPlugins },
    });
    expect(disabled.find('.v-field__clearable').exists()).toBe(false);
  });
});
