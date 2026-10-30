import { describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import DeleteUserLoanOutModal from '@/modules/myteam/components/DeleteUserLoanOutModal.vue';
import { deleteLoanOutEntry, deleteLoanOutSeries } from '@/modules/loanout/api/loanOutApi';

vi.mock('@/composables/useAccessControl', () => ({
  useAccessControl: () => ({
    hasPermission: () => true,
    featureFlags: { value: { LoanOut: { enabled: true } } },
  }),
}));
vi.mock('@/modules/loanout/api/loanOutApi', () => ({
  deleteLoanOutEntry: vi.fn(),
  deleteLoanOutSeries: vi.fn(),
}));

describe('DeleteUserLoanOutModal', () => {
  it.each(['event', 'series'] as const)('shows the backend explanation when deleting a loan-out %s', async (scope) => {
    const message = 'A shift exists at the loan-out location, so the loan-out cannot be updated or removed.';
    const deleteRequest = scope === 'series' ? deleteLoanOutSeries : deleteLoanOutEntry;
    vi.mocked(deleteRequest).mockResolvedValue({
      data: { value: { title: 'Conflict.', status: 409, errors: { LoanOut: [message] } } },
      error: { value: new Error('Conflict.') },
    } as never);
    const wrapper = mount(DeleteUserLoanOutModal, {
      props: { entry: { id: 1, loanOutSeriesId: 2 }, scope, loanOutLocationName: 'Destination', dateLabel: 'Dec 1' },
      global: {
        stubs: {
          UaModal: { template: '<div><slot name="alerts"/><slot/><slot name="actions"/></div>' },
          UaBtn: { template: '<button><slot/></button>' },
          UaAlert: { template: '<div role="alert"><slot/></div>' },
        },
      },
    });

    await wrapper.findAll('button')[1]!.trigger('click');
    await flushPromises();

    expect(deleteRequest).toHaveBeenCalledWith(scope === 'series' ? 2 : 1);
    expect(wrapper.get('[role="alert"]').text()).toBe(message);
    expect(wrapper.emitted('deleted')).toBeUndefined();
    expect(wrapper.emitted('close')).toBeUndefined();
    wrapper.unmount();
  });
});
