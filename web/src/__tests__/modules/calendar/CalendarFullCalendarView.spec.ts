import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import FullCalendar from '@fullcalendar/vue3';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import CalendarFullCalendarView from '@/modules/calendar/components/CalendarFullCalendarView.vue';
import UaCalendar from '@/shared/components/UaCalendar.vue';
import type { CalendarFullCalendarViewModel } from '@/modules/calendar/calendarTypes';

afterEach(() => vi.unstubAllEnvs());

const model: CalendarFullCalendarViewModel = {
  view: 'timeGridWeek',
  initialDate: '2026-07-13',
  timeZoneId: 'America/Vancouver',
  weekends: true,
  events: [
    {
      id: 'timed',
      type: 'calendar.general',
      sourceModule: 'calendar',
      title: 'Timed meeting',
      start: '2026-07-13T16:00:00Z',
      end: '2026-07-13T17:00:00Z',
      timeZoneId: 'UTC',
    },
    {
      id: 'all-day',
      type: 'calendar.holiday',
      sourceModule: 'calendar',
      title: 'Holiday',
      start: '2026-07-13',
      end: '2026-07-14',
      allDay: true,
    },
  ],
};

describe('FullCalendar viewing time zone', () => {
  it.each(['UTC', 'Asia/Tokyo'])(
    'updates mounted event wall times independently of browser zone %s',
    async (browserZone) => {
      vi.stubEnv('TZ', browserZone);
      expect(Intl.DateTimeFormat().resolvedOptions().timeZone).toBe(browserZone);
      const app = await createTestApp({ loadConfig: false });
      const wrapper = mount(CalendarFullCalendarView, { props: { model }, global: { plugins: app.mountPlugins } });
      try {
        await flushPromises();
        const calendar = wrapper.findComponent(FullCalendar);
        const api = calendar.vm.getApi();
        expect(api.getOption('timeZone')).toBe('America/Vancouver');
        expect(wrapper.get('.ua-calendar__event-type--calendar-general').text()).toMatch(/9:00\s*–\s*10:00am/);
        expect(api.getEventById('all-day')?.startStr).toBe('2026-07-13');

        await wrapper.setProps({ model: { ...model, timeZoneId: 'America/Edmonton' } });
        await flushPromises();
        expect(wrapper.findComponent(FullCalendar).vm.getApi()).toBe(api);
        expect(api.getOption('timeZone')).toBe('America/Edmonton');
        expect(wrapper.get('.ua-calendar__event-type--calendar-general').text()).toMatch(/10:00\s*–\s*11:00am/);
        expect(api.getEventById('all-day')?.startStr).toBe('2026-07-13');
        expect(api.getEventById('all-day')?.endStr).toBe('2026-07-14');

        await wrapper.get('.ua-calendar__event-type--calendar-general').trigger('click');
        expect(wrapper.emitted('eventClick')?.[0]?.[0]).toEqual(model.events[0]);

        await wrapper.setProps({
          model: { ...model, timeZoneId: 'America/Edmonton', view: 'dayGridMonth', weekends: false },
        });
        await flushPromises();
        const changedApi = wrapper.findComponent(FullCalendar).vm.getApi();
        expect(changedApi.view.type).toBe('dayGridMonth');
        expect(changedApi.getOption('weekends')).toBe(false);
      } finally {
        wrapper.unmount();
      }
    },
  );

  it('keeps custom option precedence and the explicit local fallback', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(UaCalendar, {
      props: { events: [], view: 'timeGridWeek', initialDate: '2026-07-13' },
      global: { plugins: app.mountPlugins },
    });
    try {
      const api = wrapper.findComponent(FullCalendar).vm.getApi();
      expect(api.getOption('timeZone')).toBe('local');
      await wrapper.setProps({
        timeZone: 'America/Vancouver',
        fullCalendarProps: { options: { timeZone: 'UTC', weekends: false } },
      });
      await flushPromises();
      expect(api.getOption('timeZone')).toBe('UTC');
      expect(api.getOption('weekends')).toBe(false);
    } finally {
      wrapper.unmount();
    }
  });
});
