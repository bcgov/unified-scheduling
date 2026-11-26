import { mount, flushPromises } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '../../helpers/createTestApp';
import { server } from '../../mocks/server';
import LeaveForm, { type LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';

const EXPIRING_LEAVE_TYPE_ID = 1;

describe('LeaveForm leave type expiry', () => {
  async function mountForm(date: string, hasLeaveType = true) {
    server.use(
      http.get('*/api/leave/leave-types', () =>
        HttpResponse.json([
          {
            id: EXPIRING_LEAVE_TYPE_ID,
            name: 'Vacation/CTO',
            effectiveDateUtc: '2020-06-10T00:00:00Z',
            expiryDateUtc: '2026-11-26T00:00:00Z',
          },
        ]),
      ),
    );
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(LeaveForm, {
      props: {
        modelValue: {
          allDay: true,
          date,
          leaveType: hasLeaveType ? EXPIRING_LEAVE_TYPE_ID : null,
        },
        showRepeat: false,
        'onUpdate:modelValue': (value: LeaveFormData) => wrapper.setProps({ modelValue: value }),
      },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();
    return wrapper;
  }

  it('keeps the leave type selectable and valid for a date before its expiry', async () => {
    const wrapper = await mountForm('2026-11-25');
    const select = wrapper.findComponent({ name: 'UaSelect' });
    expect(select.props('items')).toEqual([expect.objectContaining({ code: EXPIRING_LEAVE_TYPE_ID })]);
    expect(wrapper.props('modelValue')).toMatchObject({ leaveType: EXPIRING_LEAVE_TYPE_ID });
    expect(wrapper.text()).not.toContain('not active for this date');
    expect(wrapper.emitted('validation-change')?.at(-1)).toEqual([false]);
    wrapper.unmount();
  });

  it.each(['2026-11-26', '2026-12-01'])('does not offer the expired leave type for %s', async (date) => {
    const wrapper = await mountForm(date, false);
    const select = wrapper.findComponent({ name: 'UaSelect' });
    expect(select.props('items')).toEqual([]);
    wrapper.unmount();
  });

  it('clears a selected leave type when the date moves to or past its expiry', async () => {
    const wrapper = await mountForm('2026-11-25');
    await wrapper.setProps({ modelValue: { ...wrapper.props('modelValue'), date: '2026-11-26' } });
    await flushPromises();
    expect(wrapper.props('modelValue')).toMatchObject({ date: '2026-11-26', leaveType: null });
    wrapper.unmount();
  });
});
