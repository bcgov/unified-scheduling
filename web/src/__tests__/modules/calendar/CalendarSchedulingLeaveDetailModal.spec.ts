import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import type { LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import CalendarSchedulingLeaveDetailModal from '@/modules/scheduling/CalendarSchedulingLeaveDetailModal.vue';
import LeaveDetailsPanel from '@/modules/leave/components/LeaveDetailsPanel.vue';
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

async function mountModal(event = createLeaveEvent()) {
  const app = await createTestApp({ loadConfig: false, permissions: [Permissions.LeaveEdit] });
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

  it('loads audit details for a standalone leave entry', async () => {
    const event = createLeaveEvent();
    event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    const createdOn = '2026-06-01T12:00:00Z';
    server.use(http.get('*/api/scheduling/leave/entries/42', () => HttpResponse.json({ ...entry, createdOn })));
    const wrapper = await mountModal(event);
    await vi.waitFor(() => expect(wrapper.getComponent(LeaveDetailsPanel).props('audit')).toMatchObject({ createdOn }));
    wrapper.unmount();
  });

  it('uses the calendar event time zone for audit details when the selected entry has none', async () => {
    const event = createLeaveEvent();
    event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    server.use(http.get('*/api/scheduling/leave/entries/42', () => HttpResponse.json({ ...entry, timeZoneId: null })));
    const wrapper = await mountModal(event);
    await vi.waitFor(() => expect(wrapper.getComponent(LeaveDetailsPanel).props('audit')).toMatchObject({ id: 42 }));
    expect(wrapper.getComponent(LeaveDetailsPanel).props('timeZone')).toBe('America/Vancouver');
    wrapper.unmount();
  });

  it('aborts an in-flight entry request when the modal closes', async () => {
    const event = createLeaveEvent();
    event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    let started = false;
    let aborted = false;
    server.use(
      http.get('*/api/scheduling/leave/entries/42', async ({ request }) => {
        started = true;
        await new Promise<void>((resolve) => {
          request.signal.addEventListener(
            'abort',
            () => {
              aborted = true;
              resolve();
            },
            { once: true },
          );
        });
        return HttpResponse.json(entry);
      }),
    );
    const wrapper = await mountModal(event);
    await vi.waitFor(() => expect(started).toBe(true));
    wrapper.unmount();
    await vi.waitFor(() => expect(aborted).toBe(true));
  });

  it('does not allow editing when the leave entry fails to load', async () => {
    const event = createLeaveEvent();
    event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    server.use(http.get('*/api/scheduling/leave/entries/42', () => new HttpResponse(null, { status: 500 })));
    const wrapper = await mountModal(event);
    await vi.waitFor(() => expect(wrapper.findComponent(LeaveDetailsPanel).props('isLoading')).toBe(false));
    const vm = wrapper.vm as unknown as { selectTab: (tab: 'edit') => void };
    vm.selectTab('edit');
    await flushPromises();
    expect(wrapper.findComponent({ name: 'LeaveForm' }).exists()).toBe(false);
    wrapper.unmount();
  });

  it.each(['event', 'series'] as const)(
    'restores the scope choice when the %s request fails and allows retry',
    async (scope) => {
      let requests = 0;
      const url = scope === 'event' ? '*/api/scheduling/leave/entries/42' : '*/api/scheduling/leave/series/77';
      server.use(
        http.get(url, () => {
          requests++;
          return requests === 1 ? new HttpResponse(null, { status: 500 }) : HttpResponse.json(entry);
        }),
      );
      const wrapper = await mountModal();
      const vm = wrapper.vm as unknown as { selectedOpenScope: 'event' | 'series' | null };
      vm.selectedOpenScope = scope;
      await vi.waitFor(() => {
        expect(requests).toBe(1);
        expect(wrapper.find('.leave-modal__scope-choice').exists()).toBe(true);
      });
      expect(vm.selectedOpenScope).toBeNull();
      vm.selectedOpenScope = scope;
      await vi.waitFor(() => expect(requests).toBe(2));
      await vi.waitFor(() => expect(wrapper.findComponent(LeaveDetailsPanel).props('audit')).toBeTruthy());
      wrapper.unmount();
    },
  );

  async function mountEntryEditor(loaded: boolean, endAtUtc: string | null) {
    const event = createLeaveEvent();
    event.start = '2026-06-11T02:00:00Z'; // 19:00 in Vancouver, after the default end time.
    event.end = endAtUtc ?? undefined;
    if (!loaded) event.metadata = { ...event.metadata, leaveSeriesId: undefined };
    const savedRequests: { saved?: unknown } = {};
    let requestedEntry = false;
    server.use(
      http.get('*/api/scheduling/leave/entries/42', () => {
        requestedEntry = true;
        return HttpResponse.json({
          ...entry,
          startAtUtc: event.start,
          endAtUtc,
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
      selectedOpenScope: 'event' | 'series' | null;
      selectedEntry: unknown;
      selectTab: (tabId: 'edit') => void;
      editFormData: LeaveFormData;
      handleSave: () => Promise<void>;
    };
    if (loaded) vm.selectedOpenScope = 'event';
    await vi.waitFor(() => expect(vm.selectedEntry).toBeTruthy());
    expect(requestedEntry).toBe(true);
    vm.selectTab('edit');
    await flushPromises();
    return { wrapper, vm, savedRequests };
  }

  it.each([true, false])('preserves a null end when editing unrelated fields (loaded entry: %s)', async (loaded) => {
    const { wrapper, vm, savedRequests } = await mountEntryEditor(loaded, null);
    try {
      expect(vm.editFormData.startTime).toBe('19:00');
      expect(vm.editFormData.endTime).toBeNull();
      expect((wrapper.get('input[aria-label="End Time"]').element as HTMLInputElement).value).toBe('');
      await wrapper.get('textarea#user-leave-notes').setValue('Updated notes');
      await vm.handleSave();
      expect(savedRequests.saved).toMatchObject({
        startAtUtc: '2026-06-11T02:00:00Z',
        endAtUtc: null,
        notes: 'Updated notes',
      });
    } finally {
      wrapper.unmount();
    }
  });

  it.each([true, false])('persists an end selected on an entry that had no end (loaded entry: %s)', async (loaded) => {
    const { wrapper, vm, savedRequests } = await mountEntryEditor(loaded, null);
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
    'preserves an existing end time when saved without changes (loaded entry: %s)',
    async (loaded) => {
      const { wrapper, vm, savedRequests } = await mountEntryEditor(loaded, '2026-06-11T03:00:00Z');
      try {
        expect(vm.editFormData.endTime).toBe('20:00');
        expect((wrapper.get('input[aria-label="End Time"]').element as HTMLInputElement).value).toBe('8:00 PM');
        await vm.handleSave();
        expect(savedRequests.saved).toMatchObject({
          endAtUtc: '2026-06-11T03:00:00Z',
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

  it('sends the series ID when updating one occurrence from a leave series', async () => {
    let saved: unknown;
    server.use(
      http.put('*/api/scheduling/leave/entries/42', async ({ request }) => {
        saved = await request.json();
        return HttpResponse.json(entry);
      }),
    );
    const wrapper = await mountModal();
    const vm = wrapper.vm as unknown as {
      selectedOpenScope: 'event' | 'series' | null;
      selectedEntry: unknown;
      selectTab: (tabId: 'edit') => void;
      handleSave: () => Promise<void>;
    };

    vm.selectedOpenScope = 'event';
    await vi.waitFor(() => expect(vm.selectedEntry).toBeTruthy());
    vm.selectTab('edit');
    await flushPromises();
    expect(wrapper.getComponent({ name: 'LeaveForm' }).props('showRequired')).toBe(true);
    await vm.handleSave();
    await flushPromises();

    expect(saved).toMatchObject({ leaveSeriesId: 77 });
    wrapper.unmount();
  });

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
      selectedOpenScope: 'event' | 'series' | null;
      selectedEntry: unknown;
      selectTab: (tabId: 'edit') => void;
    };

    vm.selectedOpenScope = 'event';
    await vi.waitFor(() => expect(vm.selectedEntry).toBeTruthy());
    vm.selectTab('edit');
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
      const vm = wrapper.vm as unknown as { selectedOpenScope: 'event' | 'series' | null };
      vm.selectedOpenScope = scope;
      await flushPromises();

      await vi.waitFor(() => {
        expect(wrapper.getComponent(LeaveDetailsPanel).props('timeZone')).toBe(
          scope === 'series' ? 'America/Toronto' : 'America/Vancouver',
        );
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
