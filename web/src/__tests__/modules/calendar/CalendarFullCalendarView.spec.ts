import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import CalendarFullCalendarView from '@/modules/calendar/components/CalendarFullCalendarView.vue';
import UaCalendar from '@/shared/components/UaCalendar.vue';
import { buildCalendarDefaultViewModel } from '@/modules/calendar/views/calendarViewModels';
import type { CalendarDataResponse } from '@/modules/calendar/calendarTypes';

describe('CalendarFullCalendarView timezone integration', () => {
  it('forwards location timezone changes while preserving timed instants and all-day date values', async () => {
    const timed = {
      id: 'training.1',
      sourceModule: 'training',
      type: 'training.user-training',
      title: 'Training',
      start: '2026-07-01T16:00:00Z',
      end: '2026-07-01T17:00:00Z',
      allDay: false,
    };
    const allDay = {
      ...timed,
      id: 'calendar.1',
      sourceModule: 'calendar',
      type: 'calendar.general',
      start: '2026-07-01',
      end: '2026-07-02',
      allDay: true,
    };
    const response: CalendarDataResponse = {
      contributions: {
        'training.events': { moduleId: 'training', contributionId: 'training.events', events: [timed, allDay] },
      },
    };
    const buildModel = (timeZoneId: string) =>
      buildCalendarDefaultViewModel(
        response,
        {
          startDate: '2026-07-01',
          endDate: '2026-07-02',
          filters: { timeZoneId },
        },
        { featureFlags: {} },
        'day',
      );
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarFullCalendarView, {
      props: { model: buildModel('America/Vancouver') },
      global: { plugins: app.mountPlugins, stubs: { UaCalendar: true } },
    });
    const calendar = wrapper.getComponent(UaCalendar);
    expect(calendar.props('fullCalendarProps')?.options?.timeZone).toBe('America/Vancouver');
    expect(calendar.props('events')).toEqual([
      expect.objectContaining({ start: timed.start, end: timed.end, allDay: false }),
      expect.objectContaining({ start: allDay.start, end: allDay.end, allDay: true }),
    ]);

    await wrapper.setProps({ model: buildModel('America/Edmonton') });
    expect(calendar.props('fullCalendarProps')?.options?.timeZone).toBe('America/Edmonton');
    expect(calendar.props('events')[0]?.start).toBe(timed.start);
    expect(calendar.props('events')[1]?.start).toBe(allDay.start);
    wrapper.unmount();
  });
});
