import { afterEach, describe, expect, it } from 'vitest';
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { getGetApiLeaveLeaveTypesMockHandler } from '@/api-access/generated/leave-type/leave-type.msw';
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import LeaveForm from '@/modules/leave/components/LeaveForm.vue';
import CalendarSchedulingShiftForm from '@/modules/scheduling/CalendarSchedulingShiftForm.vue';
import CalendarSchedulingAddResourceModal from '@/modules/scheduling/CalendarSchedulingAddResourceModal.vue';
import UaRequiredIndicator from '@/shared/components/UaRequiredIndicator.vue';

enableAutoUnmount(afterEach);

describe('Calendar form required indicators', () => {
  it('enables required indicators on the leave form in the add-resource modal', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: { initialDate: '2026-07-03', timeZone: 'America/Vancouver' },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: { template: '<section><slot /></section>' },
          CalendarSchedulingResourceTabs: {
            template: '<section><slot name="schedule" /><slot name="leave" /></section>',
          },
          CalendarSchedulingShiftForm: true,
          LeaveForm: true,
        },
      },
    });
    expect(wrapper.getComponent(LeaveForm).props('showRequired')).toBe(true);
    await flushPromises();
  });
  it.each([false, true])('marks shift fields and linked users with series scope %s', async (series) => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingShiftForm, {
      props: {
        modelValue: {
          date: '2026-07-03',
          startTime: '09:00',
          endTime: '17:00',
          repeatMode: series ? 'custom' : 'never',
          recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=3',
          publish: 'no',
          cancel: 'no',
          locationId: 12,
          userIds: ['user-1'],
          assignmentEntryLinks: [{ assignmentEntryId: 1, assignedUserIds: ['user-1'] }],
          assignmentSeriesLinks: [{ assignmentSeriesId: 2, assignedUserIds: ['user-1'] }],
        },
        locationOptions: [{ code: 12, description: 'HQ' }],
        employeeOptions: [{ code: 'user-1', description: 'Alex' }],
        showSeriesAssignment: series,
      },
      global: { plugins: app.mountPlugins },
    });
    for (const id of ['date', 'location', 'employee']) {
      expect(wrapper.get(`label[for="shift-form-${id}"]`).text()).toContain('(required)');
      expect(wrapper.get(`input#shift-form-${id}`).attributes('aria-required')).toBe('true');
    }
    for (const label of ['Start Time', 'End Time']) {
      expect(wrapper.get(`input[aria-label="${label}"]`).attributes('aria-required')).toBe('true');
    }
    for (const caption of wrapper.findAll('.shift-form__time-caption')) {
      expect(caption.text()).toContain('(required)');
    }
    expect(wrapper.get('.shift-form__link-section label').text()).toContain('(required)');
    expect(wrapper.get('.shift-form__link-section input[role="combobox"]').attributes('aria-required')).toBe('true');
    expect(wrapper.get(`label[for="shift-form-${series ? 'series-assignment' : 'assignment'}"]`).text()).not.toContain(
      '(required)',
    );
    expect(wrapper.get('label[for="shift-form-notes"]').text()).not.toContain('(required)');
    expect(wrapper.findComponent(RRuleEditor).exists()).toBe(series);
    if (series) expect(wrapper.getComponent(RRuleEditor).props('showRequired')).toBe(true);
  });

  it('opts leave fields into required display and requires an end time only for series', async () => {
    server.use(getGetApiLeaveLeaveTypesMockHandler([]));
    const app = await createTestApp({ loadConfig: false });
    const modelValue = { date: '2026-07-03', repeatMode: 'never' as const };
    const wrapper = mount(LeaveForm, {
      props: { modelValue, showEmployees: true, showRepeat: true },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();
    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
    await wrapper.setProps({ showRequired: true });
    for (const id of ['employee', 'type', 'date']) {
      expect(wrapper.get(`label[for="user-leave-${id}"]`).text()).toContain('(required)');
      expect(wrapper.get(`input#user-leave-${id}`).attributes('aria-required')).toBe('true');
    }
    expect(wrapper.get('input[aria-label="Start Time"]').attributes('aria-required')).toBe('true');
    expect(wrapper.get('input[aria-label="End Time"]').attributes('aria-required')).toBeUndefined();
    expect(wrapper.get('label[for="user-leave-notes"]').text()).not.toContain('(required)');
    await wrapper.setProps({
      modelValue: { date: '2026-07-03', repeatMode: 'custom', recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=3' },
    });
    expect(wrapper.get('input[aria-label="End Time"]').attributes('aria-required')).toBe('true');
    expect(wrapper.getComponent(RRuleEditor).props('showRequired')).toBe(true);
    await wrapper.setProps({ showEmployees: false });
    expect(wrapper.find('label[for="user-leave-employee"]').exists()).toBe(false);
  });

  it.each([
    ['RRULE:FREQ=DAILY;COUNT=3', ['frequency', 'interval', 'count']],
    ['RRULE:FREQ=WEEKLY;BYDAY=FR;COUNT=3', ['frequency', 'interval', 'count']],
    ['RRULE:FREQ=MONTHLY;BYMONTHDAY=3;COUNT=3', ['frequency', 'interval', 'month-day', 'count']],
    [
      'RRULE:FREQ=MONTHLY;BYDAY=MO;BYSETPOS=1;COUNT=3',
      ['frequency', 'interval', 'nth-position', 'nth-weekday', 'count'],
    ],
  ])('marks the visible controls of %s only when opted in', async (rule, ids) => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(RRuleEditor, {
      props: { modelValue: rule as string, startDate: '2026-07-03' },
      global: { plugins: app.mountPlugins },
    });
    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
    await wrapper.setProps({ showRequired: true });
    for (const id of ids) {
      expect(wrapper.get(`label[for="rrule-editor-${id}"]`).text()).toContain('(required)');
      expect(wrapper.get(`input#rrule-editor-${id}`).attributes('aria-required')).toBe('true');
    }
    if (rule.includes('WEEKLY')) expect(wrapper.get('#rrule-editor-weekdays-label').text()).toContain('(required)');
    await wrapper.setProps({ readOnly: true });
    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
  });

  it('marks the recurrence end date when ending on a date', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(RRuleEditor, {
      props: { startDate: '2026-07-03', untilDate: '2026-07-10', showRequired: true },
      global: { plugins: app.mountPlugins },
    });
    expect(wrapper.get('label[for="rrule-editor-until"]').text()).toContain('(required)');
    expect(wrapper.get('input#rrule-editor-until').attributes('aria-required')).toBe('true');
    expect(wrapper.find('input#rrule-editor-count').exists()).toBe(false);
  });
});
