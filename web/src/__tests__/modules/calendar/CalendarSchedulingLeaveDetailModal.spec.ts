import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
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

function createLeaveEvent(): CalendarEventBase {
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
  } as CalendarEventBase;
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
      http.get('*/api/leave/leave-types', () => HttpResponse.json([])),
    );
  });

  afterEach(() => {
    document.body.innerHTML = '';
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
      selectOpenScope: (scope: 'event') => Promise<void>;
      selectTab: (tabId: 'edit') => void;
      handleSave: () => Promise<void>;
    };

    await vm.selectOpenScope('event');
    vm.selectTab('edit');
    await vm.handleSave();
    await flushPromises();

    expect(saved).toMatchObject({ leaveSeriesId: 77 });
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
