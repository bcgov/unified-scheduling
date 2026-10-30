import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { mount, flushPromises } from '@vue/test-utils';
import { server } from '../../mocks/server';
import { createTestApp } from '../../helpers/createTestApp';
import { calendarSchedulingEventsContribution } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import { buildCalendarSchedulingViewModel } from '@/modules/scheduling/calendarSchedulingMappers';
import { buildCalendarDefaultViewModel } from '@/modules/calendar/views/calendarViewModels';
import { CALENDAR_ALL_DAY_LABEL } from '@/utils/date';
import { formatCalendarConflictEventRange } from '@/modules/calendar/calendarConflictFormatting';
import UserLeaveModal from '@/modules/myteam/components/UserLeaveModal.vue';
import LeaveForm from '@/modules/leave/components/LeaveForm.vue';
import { calendarSchedulingActionIds } from '@/modules/scheduling/calendarSchedulingActionIds';
import type { CalendarConflict } from '@/modules/calendar/calendarTypes';

describe('all-day leave calendar presentation', () => {
  const context = { startDate: '2026-06-01', endDate: '2026-06-08', filters: {} };
  const userId = '11111111-1111-1111-1111-111111111111';

  it('shows leave conflicts on a standalone shift even when leave details are unavailable', () => {
    const conflict: CalendarConflict = {
      id: 'leave-shift',
      resourceId: userId,
      isOverridden: false,
      reportOnEntry: false,
      reportOnOverlaps: true,
      overrideNote: null,
      overlapStart: '2026-06-01T16:00:00Z',
      overlapEnd: '2026-06-01T17:00:00Z',
      createdById: null,
      createdOn: null,
      updatedById: null,
      updatedOn: null,
      entry: {
        eventId: '123',
        sourceModule: 'leave',
        title: 'Leave',
        start: '2026-06-01T07:00:00Z',
        end: '2026-06-02T07:00:00Z',
        allDay: true,
        timeZoneId: 'America/Vancouver',
        sourceEntityId: 42,
      },
      overlaps: {
        eventId: '20',
        sourceModule: 'scheduling',
        title: 'Shift',
        start: '2026-06-01T16:00:00Z',
        end: '2026-06-01T17:00:00Z',
        timeZoneId: 'America/Vancouver',
        sourceEntityId: 1,
      },
    };
    const model = buildCalendarSchedulingViewModel(
      {
        contributions: {
          'calendar.events': {
            moduleId: 'calendar',
            contributionId: 'calendar.events',
            events: [],
            data: { conflicts: [conflict] },
          },
          'scheduling.events': {
            moduleId: 'scheduling',
            contributionId: 'scheduling.events',
            events: [
              {
                id: 'shift-1',
                type: 'scheduling.shift',
                sourceModule: 'scheduling',
                title: 'Shift',
                start: conflict.overlaps.start,
                end: conflict.overlaps.end,
                timeZoneId: 'America/Vancouver',
                resourceIds: [userId],
                metadata: { shiftEntryId: '1', eventId: 20 },
              } as never,
            ],
            resources: [
              { id: userId, type: 'user', sourceModule: 'scheduling', label: 'Employee', title: 'Employee' } as never,
            ],
          },
        },
      },
      context,
      'week',
    );
    const header = model.cells.find((cell) => cell.date === '2026-06-01')?.headers?.[0];
    expect(header?.conflicts).toEqual([{ conflict, currentEventId: 20 }]);
    expect(header?.action?.actionId).toBe(calendarSchedulingActionIds.showConflict);
  });

  it.each([
    ['Asia/Tokyo', '2026-05-31T15:00:00Z', '2026-06-01'],
    ['America/Vancouver', '2026-06-01T07:00:00Z', '2026-06-01'],
    ['Asia/Tokyo', '2026-05-31T15:00:00Z', undefined],
  ])('keeps leave on its local date in %s', async (timeZoneId, start, leaveDate) => {
    await createTestApp({ loadConfig: false });
    server.use(
      http.post('*/api/scheduling/calendar/events', () =>
        HttpResponse.json({
          events: [
            {
              id: 'scheduling.leave-entry.42',
              type: 'scheduling.leave',
              sourceModule: 'leave',
              eventId: 123,
              title: 'Leave',
              leaveTypeName: 'Vacation',
              leaveTypeId: 1,
              leaveEntryId: 42,
              start,
              end: '2026-06-02T07:00:00Z',
              timeZoneId,
              leaveDate,
              allDay: true,
              userIds: [userId],
            },
          ],
        }),
      ),
    );
    const data = await calendarSchedulingEventsContribution.load(context);
    expect(data.events[0]).toMatchObject({ start: '2026-06-01', end: '2026-06-02', allDay: true });
    const response = {
      contributions: {
        'scheduling.events': {
          ...data,
          resources: [
            {
              id: userId,
              type: 'user',
              sourceModule: 'scheduling',
              label: 'Employee',
              title: 'Employee',
            },
          ],
        },
      },
    };
    const model = buildCalendarSchedulingViewModel(response, context, 'week');
    const cells = model.cells.filter((cell) => cell.headers?.some((header) => header.id === data.events[0]?.id));
    expect(cells.map((cell) => cell.date)).toEqual(['2026-06-01']);
    expect(cells[0]?.headers?.[0]).toMatchObject({
      text: 'Vacation',
      subtitle: CALENDAR_ALL_DAY_LABEL,
      actionId: calendarSchedulingActionIds.viewLeaveDetails,
    });
    const fullCalendar = buildCalendarDefaultViewModel(response, context, { featureFlags: {} }, 'week');
    expect(fullCalendar.events[0]).toMatchObject({ start: '2026-06-01', end: '2026-06-02', allDay: true });
  });

  it('loads the canonical date and mode for a My Team edit', async () => {
    const app = await createTestApp({ loadConfig: false });
    server.use(
      http.get('*/api/scheduling/leave/entries/42', () =>
        HttpResponse.json({
          id: 42,
          leaveTypeId: 1,
          allDay: true,
          leaveDate: '2026-06-01',
          timeZoneId: 'Asia/Tokyo',
          startAtUtc: '2026-05-31T15:00:00Z',
          endAtUtc: '2026-06-01T15:00:00Z',
          userIds: [userId],
          locationId: 5,
        }),
      ),
    );
    const wrapper = mount(UserLeaveModal, {
      props: { user: { id: userId } as never, entry: { id: 42 }, scope: 'event' },
      global: {
        plugins: app.mountPlugins,
        stubs: { UaModal: { template: '<div><slot /></div>' }, LeaveFormComponent: true },
      },
    });
    await flushPromises();
    expect(wrapper.findComponent(LeaveForm).props('modelValue')).toMatchObject({
      date: '2026-06-01',
      allDay: true,
      startTime: '09:00',
      endTime: '17:00',
    });
    wrapper.unmount();
  });

  it('shows the operational date and All day for a cross-timezone conflict', () => {
    const label = formatCalendarConflictEventRange(
      {
        eventId: '123',
        sourceModule: 'leave',
        title: 'Leave',
        start: '2026-05-31T15:00:00Z',
        end: '2026-06-01T15:00:00Z',
        timeZoneId: 'Asia/Tokyo',
        allDay: true,
        sourceEntityId: 42,
      },
      'America/Vancouver',
    );
    expect(label).toContain(CALENDAR_ALL_DAY_LABEL);
    expect(label).not.toContain('12:00');
    expect(label).toContain('June 1, 2026');
  });
});
