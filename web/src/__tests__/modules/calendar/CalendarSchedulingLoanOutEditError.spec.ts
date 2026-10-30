import { describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import CalendarSchedulingLoanOutDetailModal from '@/modules/scheduling/CalendarSchedulingLoanOutDetailModal.vue';
import { getLoanOutSeries, updateLoanOutSeries } from '@/modules/loanout/api/loanOutApi';

vi.mock('@/composables/useAccessControl', () => ({
  useAccessControl: () => ({ hasPermission: () => true, featureFlags: { value: { LoanOut: { enabled: true } } } }),
}));
vi.mock('@/modules/calendar/calendarStore', () => ({ useCalendarStore: () => ({ refresh: vi.fn() }) }));
vi.mock('@/stores/Users', () => ({
  useUsersStore: () => ({ allUsers: [], ensureAllUsers: vi.fn().mockResolvedValue([]), getUserById: vi.fn() }),
}));
vi.mock('@/stores/LocationsStore', () => ({ useLocationsStore: () => ({ entitiesMap: {} }) }));
vi.mock('@/modules/scheduling/calendarSchedulingData', () => ({ isCalendarSchedulingEvent: () => true }));
vi.mock('@/modules/loanout/api/loanOutApi', () => ({
  getLoanOutSeries: vi.fn(),
  getLoanOutEntry: vi.fn(),
  updateLoanOutSeries: vi.fn(),
  updateLoanOutEntry: vi.fn(),
  deleteLoanOutSeries: vi.fn(),
  deleteLoanOutEntry: vi.fn(),
}));

describe('calendar loan-out series edit errors', () => {
  it('renders errors.LoanOut from the response body after saving an edited series', async () => {
    const message = 'A shift exists at the loan-out location, so the loan-out cannot be updated or removed.';
    vi.mocked(getLoanOutSeries).mockResolvedValue({
      data: {
        value: {
          id: 2,
          statusTypeCode: 'Draft',
          locationId: 5,
          loanOutLocationId: 10,
          userIds: ['11111111-1111-4111-8111-111111111111'],
          startAtUtc: '2026-11-15T07:00:00Z',
          endAtUtc: '2026-11-16T07:00:00Z',
          timeZoneId: 'America/Vancouver',
          recurrenceRule: 'RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,TU,WE,TH,FR;UNTIL=20261219T235959Z',
        },
      },
      error: { value: null },
    } as never);
    vi.mocked(updateLoanOutSeries).mockResolvedValue({
      data: { value: { title: 'Conflict.', status: 409, errors: { LoanOut: [message] } } },
      error: { value: new Error('Conflict.') },
    } as never);
    const wrapper = mount(CalendarSchedulingLoanOutDetailModal, {
      props: {
        event: {
          id: 'loanout.1',
          title: 'Loan-out',
          type: 'loanout',
          sourceModule: 'loanout',
          start: '2026-11-15T07:00:00Z',
          metadata: { loanOutEntryId: 1, loanOutSeriesId: 2 },
        },
      } as never,
      global: {
        stubs: {
          UaModal: { template: '<div><slot name="alerts"/><slot/><slot name="actions"/></div>' },
          UaBtn: { template: '<button><slot/></button>' },
          UaAlert: { template: '<div role="alert"><slot/></div>' },
          LoanOutForm: true,
          VCheckbox: true,
          CalendarSchedulingShiftDetailsPanel: true,
        },
      },
    });
    await wrapper.findAll('button')[1]!.trigger('click');
    await flushPromises();
    await wrapper.findAll('[role="tab"]')[1]!.trigger('click');
    const save = wrapper.findAll('button').find((button) => button.text() === 'Save');
    await save!.trigger('click');
    await flushPromises();

    expect(updateLoanOutSeries).toHaveBeenCalledWith(2, expect.objectContaining({ loanOutLocationId: 10 }));
    expect(wrapper.get('[role="alert"]').text()).toBe(message);
    expect(wrapper.emitted('close')).toBeUndefined();
    wrapper.unmount();
  });
});
