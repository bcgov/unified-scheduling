import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import type { LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import CalendarSchedulingLeaveDetailModal from '@/modules/scheduling/CalendarSchedulingLeaveDetailModal.vue';
import LeaveDetailsPanel from '@/modules/leave/components/LeaveDetailsPanel.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { formatCalendarEventTimeRange } from '@/utils/date';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const entry = {
  id: 42,
  leaveSeriesId: 77,
  userIds: ['00000000-0000-0000-0000-000000000001'],
  leaveTypeId: 9,
  startAtUtc: '2026-06-10T17:00:00Z',
  endAtUtc: '2026-06-10T18:00:00Z',
  timeZoneId: 'America/Vancouver',
  locationId: 3,
  statusTypeCode: 'Draft',
};

function createLeaveEvent(): CalendarSchedulingEvent {
  return {
    id: 'leave-entry.42',
    type: 'scheduling.leave',
    sourceModule: 'scheduling',
    title: 'Leave',
    start: entry.startAtUtc,
    end: entry.endAtUtc,
    timeZoneId: entry.timeZoneId,
    statusTypeCode: 'Draft',
    locationId: 3,
    resourceIds: entry.userIds,
    metadata: { leaveEntryId: '42', leaveSeriesId: '77', leaveTypeId: 9, userIds: entry.userIds },
  };
}

async function mountModal(
  event = createLeaveEvent(),
  permissions: Permissions[] = [Permissions.LeaveEdit],
  viewingLocationId: number | null = null,
) {
  const app = await createTestApp({ loadConfig: false, permissions });
  useLocationsStore(app.pinia).setSelectedLocationId(viewingLocationId);
  return mount(CalendarSchedulingLeaveDetailModal, {
    props: { event },
    global: {
      plugins: app.mountPlugins,
      stubs: {
        UaAlert: true,
        UaBtn: true,
        UaModal: defineComponent({ template: '<div><slot /></div>' }),
        LeaveDetailsPanel: true,
        LeaveForm: true,
      },
    },
  });
}

describe('CalendarSchedulingLeaveDetailModal', () => {
  beforeEach(() => {
    server.use(
      http.get('*/api/scheduling/leave/entries/42', () => HttpResponse.json(entry)),
      http.get('*/api/users*', () => HttpResponse.json([])),
      http.get('*/api/leave/leave-types', () => HttpResponse.json([{ id: 9, name: 'Vacation' }])),
    );
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  async function mountEntryEditor(inSeries: boolean, endAtUtc: string | null, startAtUtc = '2026-06-11T02:00:00Z') {
    const event = createLeaveEvent();
    event.description = 'Leave type description';
    if (!inSeries) event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    const savedRequests: { saved?: unknown } = {};
    let requestedEntry = false;
    server.use(
      http.get('*/api/scheduling/leave/entries/42', () => {
        requestedEntry = true;
        return HttpResponse.json({
          ...entry,
          leaveSeriesId: inSeries ? entry.leaveSeriesId : null,
          startAtUtc,
          endAtUtc,
          description: 'Persisted entry comment',
          notes: 'Persisted notes',
        });
      }),
      http.put('*/api/scheduling/leave/entries/42', async ({ request }) => {
        savedRequests.saved = await request.json();
        return HttpResponse.json(entry);
      }),
    );
    const app = await createTestApp({ loadConfig: false, permissions: [Permissions.LeaveEdit] });
    const wrapper = mount(CalendarSchedulingLeaveDetailModal, {
      props: { event },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: { template: '<section><slot name="alerts" /><slot /><slot name="actions" /></section>' },
          LeaveDetailsPanel: true,
        },
      },
    });
    const vm = wrapper.vm as unknown as {
      selectOpenScope: (scope: 'event') => Promise<void>;
      selectTab: (tabId: 'edit') => Promise<void>;
      editFormData: LeaveFormData;
      handleSave: () => Promise<void>;
    };
    if (inSeries) {
      await vm.selectOpenScope('event');
    } else {
      expect(requestedEntry).toBe(false);
    }
    await vm.selectTab('edit');
    expect(requestedEntry).toBe(true);
    await flushPromises();
    return { wrapper, vm, savedRequests };
  }

  it.each([true, false])('preserves a null end when editing notes (in series: %s)', async (inSeries) => {
    const { wrapper, vm, savedRequests } = await mountEntryEditor(inSeries, null);
    try {
      expect(vm.editFormData.startTime).toBe('19:00');
      expect(vm.editFormData.endTime).toBeNull();
      expect((wrapper.get('input[aria-label="End Time"]').element as HTMLInputElement).value).toBe('');
      await wrapper.get('textarea#user-leave-notes').setValue('Updated notes');
      await vm.handleSave();
      expect(savedRequests.saved).toMatchObject({
        startAtUtc: '2026-06-11T02:00:00Z',
        endAtUtc: null,
        description: 'Persisted entry comment',
        notes: 'Updated notes',
      });
    } finally {
      wrapper.unmount();
    }
  });

  it.each([true, false])('persists an end selected on an entry that had no end (in series: %s)', async (inSeries) => {
    const { wrapper, vm, savedRequests } = await mountEntryEditor(inSeries, null);
    try {
      const endSelect = wrapper
        .findAllComponents({ name: 'UaSelect' })
        .find((select) => select.find('input[aria-label="End Time"]').exists())!;
      endSelect.vm.$emit('update:modelValue', '20:00');
      await flushPromises();
      expect(vm.editFormData.endTime).toBe('20:00');
      await vm.handleSave();
      expect(savedRequests.saved).toMatchObject({
        endAtUtc: '2026-06-11T03:00:00Z',
      });
    } finally {
      wrapper.unmount();
    }
  });

  it.each([true, false])(
    'preserves an overnight interval and description when clearing notes (in series: %s)',
    async (inSeries) => {
      const startAtUtc = '2026-06-11T05:00:21.123456Z';
      const endAtUtc = '2026-06-11T13:00:42.654321Z';
      const { wrapper, vm, savedRequests } = await mountEntryEditor(inSeries, endAtUtc, startAtUtc);
      try {
        expect(vm.editFormData).toMatchObject({ date: '2026-06-10', endDate: '2026-06-11', endTime: '06:00' });
        expect((wrapper.get('#user-leave-end-date').element as HTMLInputElement).value).toBe('2026-06-11');
        await wrapper.get('textarea#user-leave-notes').setValue('');
        await vm.handleSave();
        expect(savedRequests.saved).toMatchObject({
          startAtUtc,
          endAtUtc,
          description: 'Persisted entry comment',
          notes: null,
        });
      } finally {
        wrapper.unmount();
      }
    },
  );

  it('retains the default end-time display for an uninitialized creation form', async () => {
    const { default: LeaveForm } = await import('@/modules/leave/components/LeaveForm.vue');
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(LeaveForm, {
      props: { modelValue: { date: '2026-06-10' } },
      global: { plugins: app.mountPlugins },
    });
    try {
      expect((wrapper.get('input[aria-label="End Time"]').element as HTMLInputElement).value).toBe('5:00 PM');
    } finally {
      wrapper.unmount();
    }
  });

  it('keeps standalone leave out of edit mode when the persisted entry cannot be loaded', async () => {
    const event = createLeaveEvent();
    event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    server.use(http.get('*/api/scheduling/leave/entries/42', () => new HttpResponse(null, { status: 404 })));
    const wrapper = await mountModal(event);
    const vm = wrapper.vm as unknown as { selectTab: (tabId: 'edit') => Promise<void> };
    await vm.selectTab('edit');
    expect(wrapper.get('[role="tab"][aria-selected="true"]').text()).toBe('Details');
    expect(wrapper.findComponent({ name: 'LeaveForm' }).exists()).toBe(false);
    wrapper.unmount();
  });

  it('sends the series ID when updating an active occurrence', async () => {
    let saved: unknown;
    server.use(
      http.get('*/api/scheduling/leave/entries/42', () => HttpResponse.json({ ...entry, statusTypeCode: 'Active' })),
      http.put('*/api/scheduling/leave/entries/42', async ({ request }) => {
        saved = await request.json();
        return HttpResponse.json(entry);
      }),
    );
    const wrapper = await mountModal();
    const vm = wrapper.vm as unknown as {
      selectOpenScope: (scope: 'event') => Promise<void>;
      selectTab: (tabId: 'edit') => Promise<void>;
      handleSave: () => Promise<void>;
    };

    await vm.selectOpenScope('event');
    await vm.selectTab('edit');
    await flushPromises();
    expect(wrapper.getComponent({ name: 'LeaveForm' }).props('showRequired')).toBe(true);
    await vm.handleSave();
    await flushPromises();

    expect(saved).toMatchObject({ leaveSeriesId: 77 });
    wrapper.unmount();
  });

  it.each([
    { status: 'Draft', allowed: true, locationId: 3, tabs: ['Details', 'Edit', 'Delete'] },
    { status: 'Active', allowed: true, locationId: 3, tabs: ['Details', 'Edit'] },
    { status: 'Cancelled', allowed: true, locationId: 3, tabs: ['Details'] },
    { status: 'Active', allowed: false, locationId: 3, tabs: ['Details'] },
    { status: 'Active', allowed: true, locationId: 5, tabs: ['Details'] },
  ])(
    'shows $tabs for $status leave at location $locationId (permission: $allowed)',
    async ({ status, allowed, locationId, tabs }) => {
      const event = createLeaveEvent();
      event.statusTypeCode = status;
      event.metadata = { ...event.metadata, leaveSeriesId: undefined };
      const wrapper = await mountModal(
        event,
        allowed ? [Permissions.LeaveEdit, Permissions.LeaveDelete] : [],
        locationId,
      );
      expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(tabs);
      wrapper.unmount();
    },
  );

  it.each([
    { status: 'Active', locked: true },
    { status: 'Draft', locked: false },
  ])(
    'preserves the $status series description and schedule without fetching occurrences',
    async ({ status, locked }) => {
      const series = {
        ...entry,
        id: 77,
        statusTypeCode: status,
        leaveEntryIds: [42],
        description: 'Persisted series comment',
        startAtUtc: '2026-06-11T01:00:21.123456Z',
        endAtUtc: '2026-06-11T09:00:42.654321Z',
        timeZoneId: 'America/Toronto',
        recurrenceRule: 'FREQ=DAILY;COUNT=1',
      };
      let saved: unknown;
      const occurrenceRequests: string[] = [];
      server.use(
        http.get('*/api/scheduling/leave/series/77', () => HttpResponse.json(series)),
        http.put('*/api/scheduling/leave/series/77', async ({ request }) => {
          saved = await request.json();
          return HttpResponse.json(series);
        }),
        http.get('*/api/scheduling/leave/users/:userId/entries', ({ request }) => {
          occurrenceRequests.push(request.url);
          return HttpResponse.json([]);
        }),
        http.get('*/api/scheduling/leave/entries/:id', ({ request }) => {
          occurrenceRequests.push(request.url);
          return HttpResponse.json(entry);
        }),
      );
      const wrapper = await mountModal();
      const vm = wrapper.vm as unknown as {
        selectOpenScope: (scope: 'series') => Promise<void>;
        handleSave: () => Promise<void>;
      };
      await vm.selectOpenScope('series');
      await wrapper.get('[role="tab"][aria-selected="false"]').trigger('click');
      await flushPromises();
      const form = wrapper.getComponent({ name: 'LeaveForm' });
      expect(form.props('recurrenceLocked')).toBe(locked);
      expect(form.props('modelValue')).toMatchObject({ date: '2026-06-10', endDate: '2026-06-11', endTime: '05:00' });
      form.vm.$emit('update:modelValue', { ...form.props('modelValue'), notes: 'Updated notes' });
      await vm.handleSave();
      expect(saved).toMatchObject({
        description: series.description,
        notes: 'Updated notes',
        startAtUtc: series.startAtUtc,
        endAtUtc: series.endAtUtc,
        timeZoneId: series.timeZoneId,
        recurrenceRule: series.recurrenceRule,
      });
      expect(occurrenceRequests).toEqual([]);
      wrapper.unmount();
    },
  );

  it.each([false, true])('offers location members on the leave date (moved: %s)', async (moved) => {
    const homeUserId = '00000000-0000-0000-0000-000000000001';
    const loanedInUserId = '00000000-0000-0000-0000-000000000099';
    server.use(
      http.get('*/api/users*', () =>
        HttpResponse.json([
          { id: homeUserId, firstName: 'Alex', lastName: 'Alpha', isEnabled: true, homeLocationId: 3 },
          {
            id: loanedInUserId,
            firstName: 'Lee',
            lastName: 'Loaned',
            isEnabled: true,
            homeLocationId: 99,
            userLocations: moved
              ? [{ locationId: 3, effectiveFrom: null, effectiveUntil: '2099-01-01T00:00:00Z' }]
              : [],
          },
        ]),
      ),
    );
    const wrapper = await mountModal();
    const vm = wrapper.vm as unknown as {
      selectOpenScope: (scope: 'event') => Promise<void>;
      selectTab: (tabId: 'edit') => Promise<void>;
    };

    await vm.selectOpenScope('event');
    await vm.selectTab('edit');
    await flushPromises();

    const options = wrapper.getComponent({ name: 'LeaveForm' }).props('employeeOptions') as { code: string }[];
    expect(options.map((option) => option.code)).toEqual(moved ? [homeUserId, loanedInUserId] : [homeUserId]);
    wrapper.unmount();
  });

  it.each(['series', 'event'] as const)(
    'shows the scoped %s hours and all-day mode through an all-day exception',
    async (scope) => {
      const event = { ...createLeaveEvent(), start: '2026-06-15', end: '2026-06-16', allDay: true };
      server.use(
        http.get('*/api/scheduling/leave/entries/42', () =>
          HttpResponse.json({ ...entry, allDay: true, leaveDate: '2026-06-15' }),
        ),
        http.get('*/api/scheduling/leave/series/77', () =>
          HttpResponse.json({
            ...entry,
            id: 77,
            allDay: false,
            startAtUtc: '2026-06-01T13:00:00Z',
            endAtUtc: '2026-06-01T21:00:00Z',
            timeZoneId: 'America/Toronto',
            recurrenceRule: 'FREQ=WEEKLY;COUNT=4',
          }),
        ),
      );
      const wrapper = await mountModal(event);
      const vm = wrapper.vm as unknown as { selectOpenScope: (scope: 'event' | 'series') => Promise<void> };
      await vm.selectOpenScope(scope);
      await flushPromises();

      await vi.waitFor(() => {
        const rows = wrapper.getComponent(LeaveDetailsPanel).props('detailRows');
        expect(rows.find((row) => row.label === 'Time')?.value).toBe(
          scope === 'series'
            ? formatCalendarEventTimeRange('2026-06-01T13:00:00Z', '2026-06-01T21:00:00Z', {
                timeZone: 'America/Toronto',
              })
            : formatCalendarEventTimeRange(event.start, event.end, { allDay: true }),
        );
      });
      wrapper.unmount();
    },
  );
});
