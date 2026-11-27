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
    expect(wrapper.find('#user-leave-end-date').exists()).toBe(false);
    expect(wrapper.findComponent({ name: 'VSwitch' }).props('modelValue')).toBe(true);
    wrapper.unmount();
  });

  it('uses the start date as the default and keeps a selected end date independent', async () => {
    const wrapper = await mountForm();
    const startDate = wrapper.get('#user-leave-date');
    const endDate = wrapper.get('#user-leave-end-date');
    expect((endDate.element as HTMLInputElement).value).toBe('2026-06-01');
    await startDate.setValue('2026-06-02');
    expect((endDate.element as HTMLInputElement).value).toBe('2026-06-02');
    await endDate.setValue('2026-06-04');
    await startDate.setValue('2026-06-03');
    expect(wrapper.props('modelValue')).toMatchObject({ date: '2026-06-03', endDate: '2026-06-04' });
    expect((endDate.element as HTMLInputElement).value).toBe('2026-06-04');
    wrapper.unmount();
  });

  it('disables recurrence fields while leaving notes editable', async () => {
    const wrapper = await mountForm();
    await wrapper.setProps({
      showRepeat: true,
      recurrenceLocked: true,
      modelValue: { ...wrapper.props('modelValue'), repeatMode: 'custom', recurrenceRule: 'FREQ=DAILY;COUNT=2' },
    });
    for (const selector of [
      '#user-leave-date',
      '#user-leave-end-date',
      'input[aria-label="Repeat"]',
      'input[aria-label="Start Time"]',
      'input[aria-label="End Time"]',
    ]) {
      expect((wrapper.get(selector).element as HTMLInputElement).disabled).toBe(true);
    }
    expect(wrapper.getComponent({ name: 'VSwitch' }).props('disabled')).toBe(true);
    expect(wrapper.getComponent({ name: 'RRuleEditor' }).props('disabled')).toBe(true);
    expect((wrapper.get('textarea#user-leave-notes').element as HTMLTextAreaElement).disabled).toBe(false);
    wrapper.unmount();
  });
});
