import { afterEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import LoanOutForm from '@/modules/loanout/components/LoanOutForm.vue';
import CalendarSchedulingLoanOutDetailModal from '@/modules/scheduling/CalendarSchedulingLoanOutDetailModal.vue';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import { useLocationsStore } from '@/stores/LocationsStore';

enableAutoUnmount(afterEach);

const homeUserId = '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62';
const loanedInUserId = '7b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d4e';

const users = [
  { id: homeUserId, idirName: 'aalpha', firstName: 'Alex', lastName: 'Alpha', isEnabled: true, homeLocationId: 12 },
  {
    id: loanedInUserId,
    idirName: 'lloaned',
    firstName: 'Lee',
    lastName: 'Loaned',
    isEnabled: true,
    homeLocationId: 99,
  },
];

async function mountEditTab(options: { eventLocationId?: number; entityLocationId: number | null }) {
  server.use(
    http.get('*/api/users*', () => HttpResponse.json(users)),
    http.get('*/api/scheduling/loan-out/entries/42', () =>
      HttpResponse.json({
        id: 42,
        statusTypeCode: 'Draft',
        locationId: options.entityLocationId,
        loanOutLocationId: 13,
        userIds: [homeUserId],
        startAtUtc: '2026-07-03T07:00:00Z',
        endAtUtc: '2026-07-04T07:00:00Z',
        timeZoneId: 'America/Vancouver',
        recurrenceRule: null,
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
    locationId: options.eventLocationId,
    metadata: { loanOutEntryId: '42' },
  };
  const wrapper = mount(CalendarSchedulingLoanOutDetailModal, {
    props: { event },
    global: {
      plugins: app.mountPlugins,
      stubs: { UaModal: { template: '<section><slot /></section>' }, LoanOutForm: true },
    },
  });
  await vi.waitFor(() => expect(wrapper.findAll('[role="tab"]')).toHaveLength(2));
  await wrapper.get('[role="tab"]:last-child').trigger('click');
  return wrapper;
}

function optionCodes(wrapper: Awaited<ReturnType<typeof mountEditTab>>) {
  return (wrapper.getComponent(LoanOutForm).props('employeeOptions') as { code: string }[]).map(
    (option) => option.code,
  );
}

describe('CalendarSchedulingLoanOutDetailModal employee options', () => {
  it('only offers employees whose home location matches the loan-out location on the edit tab', async () => {
    const wrapper = await mountEditTab({ eventLocationId: 12, entityLocationId: 12 });
    await vi.waitFor(() => expect(optionCodes(wrapper)).toEqual([homeUserId]));
  });

  it('falls back to all users when the loan-out location cannot be resolved', async () => {
    const wrapper = await mountEditTab({ entityLocationId: null });
    await vi.waitFor(() => expect(optionCodes(wrapper)).toEqual([homeUserId, loanedInUserId]));
  });
});
