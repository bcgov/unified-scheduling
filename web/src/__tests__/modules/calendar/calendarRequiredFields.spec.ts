import { afterEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import { getGetApiLeaveLeaveTypesMockHandler } from '@/api-access/generated/leave-type/leave-type.msw';
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import LeaveForm from '@/modules/leave/components/LeaveForm.vue';
import LoanOutForm from '@/modules/loanout/components/LoanOutForm.vue';
import UserLeaveModal from '@/modules/myteam/components/UserLeaveModal.vue';
import CalendarSchedulingShiftForm from '@/modules/scheduling/CalendarSchedulingShiftForm.vue';
import CalendarSchedulingAddResourceModal from '@/modules/scheduling/CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingLoanOutDetailModal from '@/modules/scheduling/CalendarSchedulingLoanOutDetailModal.vue';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import UaRequiredIndicator from '@/shared/components/UaRequiredIndicator.vue';
import { useLocationsStore } from '@/stores/LocationsStore';

enableAutoUnmount(afterEach);

describe('Calendar form required indicators', () => {
  it('enables required indicators on leave and loan-out forms in the add-resource modal', async () => {
    const app = await createTestApp({ loadConfig: false });
    useLocationsStore(app.pinia).entities = [{ id: 12, name: 'HQ' }];
    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: { initialDate: '2026-07-03', timeZone: 'America/Vancouver' },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: { template: '<section><slot /></section>' },
          CalendarSchedulingResourceTabs: {
            template: '<section><slot name="schedule" /><slot name="leave" /><slot name="loanout" /></section>',
          },
          CalendarSchedulingShiftForm: true,
          LeaveForm: true,
        },
      },
    });
    expect(wrapper.getComponent(LeaveForm).props('showRequired')).toBe(true);
    const loanOut = wrapper.getComponent(LoanOutForm);
    for (const id of ['employees', 'location', 'date']) {
      expect(loanOut.get(`label[for="loan-out-${id}"]`).text()).toContain('(required)');
      expect(loanOut.get(`input#loan-out-${id}`).attributes('aria-required')).toBe('true');
    }
    await flushPromises();
  });

  it('opts loan-out fields into required display and marks recurrence only for custom repeat', async () => {
    const app = await createTestApp({ loadConfig: false });
    useLocationsStore(app.pinia).entities = [{ id: 12, name: 'HQ' }];
    const modelValue = { date: '2026-07-03', repeatMode: 'never' as const };
    const wrapper = mount(LoanOutForm, {
      props: { modelValue, showEmployees: true, showRepeat: true },
      global: { plugins: app.mountPlugins },
    });
    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
    expect(wrapper.find('[aria-required="true"]').exists()).toBe(false);
    await wrapper.setProps({ showRequired: true });
    for (const id of ['employees', 'location', 'date']) {
      expect(wrapper.get(`label[for="loan-out-${id}"]`).text()).toContain('(required)');
      expect(wrapper.get(`input#loan-out-${id}`).attributes('aria-required')).toBe('true');
    }
    for (const id of ['all-day', 'repeat', 'notes']) {
      expect(wrapper.get(`label[for="loan-out-${id}"]`).text()).not.toContain('(required)');
      expect(wrapper.get(`input#loan-out-${id}, textarea#loan-out-${id}`).attributes('aria-required')).toBeUndefined();
    }
    expect(wrapper.findComponent(RRuleEditor).exists()).toBe(false);
    await wrapper.setProps({
      modelValue: { ...modelValue, repeatMode: 'custom', recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=3' },
    });
    for (const id of ['frequency', 'interval', 'count']) {
      expect(wrapper.get(`label[for="loan-out-recurrence-${id}"]`).text()).toContain('(required)');
      expect(wrapper.get(`input#loan-out-recurrence-${id}`).attributes('aria-required')).toBe('true');
    }
    expect(wrapper.find('[required]').exists()).toBe(false);
    await wrapper.setProps({ showRequired: false });
    expect(wrapper.findComponent(UaRequiredIndicator).exists()).toBe(false);
    expect(wrapper.find('[aria-required="true"]').exists()).toBe(false);
    await wrapper.setProps({ showRequired: true, modelValue });
    expect(wrapper.findComponent(RRuleEditor).exists()).toBe(false);
    await wrapper.setProps({ showEmployees: false });
    expect(wrapper.find('label[for="loan-out-employees"]').exists()).toBe(false);
    expect(wrapper.find('input#loan-out-employees').exists()).toBe(false);
  });

  it.each(['event', 'series'] as const)('marks required loan-out fields when editing a %s', async (scope) => {
    server.use(
      http.get('*/api/users', () => HttpResponse.json([])),
      http.get(`*/api/scheduling/loan-out/${scope === 'series' ? 'series' : 'entries'}/42`, () =>
        HttpResponse.json({
          id: 42,
          statusTypeCode: 'Draft',
          locationId: 12,
          loanOutLocationId: 13,
          userIds: [],
          startAtUtc: '2026-07-03T07:00:00Z',
          endAtUtc: '2026-07-04T07:00:00Z',
          timeZoneId: 'America/Vancouver',
          recurrenceRule: scope === 'series' ? 'RRULE:FREQ=DAILY;COUNT=3' : null,
        }),
      ),
    );
    const app = await createTestApp({
      featureFlags: { LoanOut: { enabled: true } },
      permissions: [Permissions.LoanOutEdit],
    });
    useLocationsStore(app.pinia).entities = [
      { id: 12, name: 'HQ' },
      { id: 13, name: 'Branch' },
    ];
    const event: CalendarSchedulingEvent = {
      id: 'loan-out-42',
      type: 'scheduling.loanout',
      sourceModule: 'scheduling',
      title: 'Loan Out',
      start: '2026-07-03T07:00:00Z',
      metadata: { loanOutEntryId: '42', loanOutSeriesId: scope === 'series' ? '42' : undefined },
    };
    const wrapper = mount(CalendarSchedulingLoanOutDetailModal, {
      props: { event },
      global: {
        plugins: app.mountPlugins,
        stubs: { UaModal: { template: '<section><slot /></section>' } },
      },
    });
    if (scope === 'series') {
      await wrapper.get('.loanout-modal__scope-choice-actions button:last-child').trigger('click');
    }
    await vi.waitFor(() => expect(wrapper.findAll('[role="tab"]')).toHaveLength(2));
    await wrapper.get('[role="tab"]:last-child').trigger('click');
    const form = wrapper.getComponent(LoanOutForm);
    for (const id of ['employees', 'location', 'date']) {
      expect(form.get(`label[for="loan-out-${id}"]`).text()).toContain('(required)');
      expect(form.get(`input#loan-out-${id}`).attributes('aria-required')).toBe('true');
    }
    expect(form.findComponent(RRuleEditor).exists()).toBe(scope === 'series');
    if (scope === 'series') {
      expect(form.get('label[for="loan-out-recurrence-frequency"]').text()).toContain('(required)');
      expect(form.get('input#loan-out-recurrence-frequency').attributes('aria-required')).toBe('true');
    }
    for (const id of ['all-day', 'notes']) {
      expect(form.get(`label[for="loan-out-${id}"]`).text()).not.toContain('(required)');
    }
  });

  it('enables My Team leave indicators while keeping individual end times optional', async () => {
    server.use(getGetApiLeaveLeaveTypesMockHandler([]));
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(UserLeaveModal, {
      props: {
        user: {
          id: 'user-1',
          idirName: 'alex',
          firstName: 'Alex',
          lastName: 'Example',
          email: 'alex@example.com',
          employeeNumber: '123',
          gender: 'Other',
          isEnabled: true,
        },
      },
      global: {
        plugins: app.mountPlugins,
        stubs: { UaModal: { template: '<section><slot /></section>' } },
      },
    });
    await flushPromises();
    const form = wrapper.getComponent(LeaveForm);
    for (const id of ['type', 'date']) {
      expect(form.get(`label[for="user-leave-${id}"]`).text()).toContain('(required)');
      expect(form.get(`input#user-leave-${id}`).attributes('aria-required')).toBe('true');
    }
    expect(form.get('input[aria-label="Start Time"]').attributes('aria-required')).toBe('true');
    expect(form.get('input[aria-label="End Time"]').attributes('aria-required')).toBeUndefined();
    expect(form.find('input#user-leave-employee').exists()).toBe(false);
    form.vm.$emit('update:modelValue', {
      ...form.props('modelValue'),
      date: '2026-07-03',
      repeatMode: 'custom',
      recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=3',
    });
    await flushPromises();
    expect(form.get('input[aria-label="End Time"]').attributes('aria-required')).toBe('true');
    expect(form.findAll('.user-leave__time-caption')[1]!.text()).toContain('(required)');
    expect(form.get('input#user-leave-recurrence-frequency').attributes('aria-required')).toBe('true');
    for (const id of ['repeat', 'notes']) {
      expect(form.get(`label[for="user-leave-${id}"]`).text()).not.toContain('(required)');
      expect(form.get(`input#user-leave-${id}, textarea#user-leave-${id}`).attributes('aria-required')).toBeUndefined();
    }
    expect(form.find('[required]').exists()).toBe(false);
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
