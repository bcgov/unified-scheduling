import { mount, flushPromises } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { createTestApp } from '../../helpers/createTestApp';
import LeaveForm, { type LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import { http, HttpResponse } from 'msw';
import { server } from '../../mocks/server';

describe('LeaveForm', () => {
  async function mountForm(allDay = false) {
    server.use(http.get('*/api/leave/leave-types', () => HttpResponse.json([])));
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(LeaveForm, {
      props: {
        modelValue: { allDay, date: '2026-06-01', startTime: '09:00', endTime: '17:00' },
        leaveTypeOptions: [],
        showRepeat: false,
        'onUpdate:modelValue': (value: LeaveFormData) => wrapper.setProps({ modelValue: value }),
      },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();
    return wrapper;
  }

  it('defaults to timed controls and preserves their values while toggling', async () => {
    const wrapper = await mountForm();
    expect(wrapper.find('[aria-label="Start Time"]').exists()).toBe(true);
    const toggle = wrapper.findComponent({ name: 'VSwitch' });
    toggle.vm.$emit('update:modelValue', true);
    await flushPromises();
    expect(wrapper.find('[aria-label="Start Time"]').exists()).toBe(false);
    expect(wrapper.props('modelValue')).toMatchObject({ allDay: true, startTime: '09:00', endTime: '17:00' });
    toggle.vm.$emit('update:modelValue', false);
    await flushPromises();
    expect(wrapper.find('[aria-label="Start Time"]').exists()).toBe(true);
    expect(wrapper.props('modelValue')).toMatchObject({ allDay: false, startTime: '09:00', endTime: '17:00' });
    wrapper.unmount();
  });

  it('initializes all-day edits without time controls', async () => {
    const wrapper = await mountForm(true);
    expect(wrapper.find('[aria-label="Start Time"]').exists()).toBe(false);
    expect(wrapper.find('[aria-label="End Time"]').exists()).toBe(false);
    expect(wrapper.findComponent({ name: 'VSwitch' }).props('modelValue')).toBe(true);
    wrapper.unmount();
  });
});
