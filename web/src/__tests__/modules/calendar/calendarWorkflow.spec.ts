import { mount, flushPromises } from '@vue/test-utils';
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { defineComponent } from 'vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

describe('calendar workflow', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2025-04-09T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the main calendar path, reloads for date navigation, and opens event details', async () => {
    const viewEvent = {
      id: 'evt-1',
      type: 'calendar.holiday',
      sourceModule: 'calendar',
      title: 'Spring Holiday',
      start: '2025-04-10',
      end: '2025-04-11',
      allDay: true,
      eventTypeCode: 'holiday',
      statusTypeCode: 'active',
      description: 'School closed',
      notes: 'Bring forms next week',
      locationId: 12,
    };

    const loadData = vi.fn().mockResolvedValue({
      contributions: {
        'calendar.events': {
          moduleId: 'calendar',
          contributionId: 'calendar.events',
          events: [viewEvent],
        },
      },
    });
    const cancel = vi.fn();

    const TestView = defineComponent({
      props: {
        model: {
          type: Object,
          required: true,
        },
      },
      emits: ['eventClick'],
      template: `
        <div>
          <div data-testid="loaded-events">{{ model.events.map((event) => event.title).join(', ') }}</div>
          <button type="button" data-testid="open-event" @click="$emit('eventClick', model.events[0])">
            Open event
          </button>
        </div>
      `,
    });

    vi.doMock('@/modules/calendar/calendarDataService', () => ({
      calendarDataService: { loadData, cancel },
    }));

    vi.doMock('@/modules/calendar/registry/calendarActionRegistry', () => ({
      calendarActionRegistry: {
        getCreateActions: vi.fn(() => []),
        getToolbarActionsForView: vi.fn(() => []),
        getViewDetailActions: vi.fn(() => [
          {
            id: 'detail',
            moduleId: 'calendar',
            run: ({ event }: { event: { id: string } }) => {
              selectEventById?.(event.id);
            },
          },
        ]),
      },
    }));

    vi.doMock('@/modules/calendar/registry/calendarRegistry', () => ({
      calendarRegistry: {
        getAvailableViews: vi.fn(() => [
          {
            id: 'workflow-view',
            label: 'Workflow',
            component: TestView,
            buildModel: (dataResponse: { contributions: Record<string, { events: unknown[] }> }) => ({
              events: Object.values(dataResponse.contributions).flatMap((contribution) => contribution.events),
            }),
          },
        ]),
      },
    }));

    const [
      { default: Calendar },
      { useCalendarStore },
      { useLocationsStore },
      { buildDateRangeForPeriod, formatRangeLabel, getTodayDateOnly, shiftDateRange },
    ] = await Promise.all([
      import('@/modules/calendar/Calendar.vue'),
      import('@/modules/calendar/calendarStore'),
      import('@/stores/LocationsStore'),
      import('@/utils/date'),
    ]);

    const { mountPlugins, pinia } = await createTestApp({ featureFlags: { calendarModule: true } });
    const calendarStore = useCalendarStore(pinia);
    const selectEventById = (eventId: string) => calendarStore.setSelectedEvent(eventId);
    const locationsStore = useLocationsStore(pinia);

    calendarStore.setPeriod('week');
    calendarStore.setDateRange('2025-04-07', '2025-04-14');
    locationsStore.entities = [{ id: 12, name: 'Main Hall' }];
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(Calendar, {
      attachTo: document.body,
      global: {
        plugins: mountPlugins,
      },
    });

    try {
      await flushPromises();

      expect(wrapper.get('[role="tab"]').text()).toBe('Workflow');
      expect(wrapper.text()).toContain(formatRangeLabel('2025-04-07', '2025-04-14', 'week'));
      expect(wrapper.get('[data-testid="loaded-events"]').text()).toContain('Spring Holiday');
      expect(loadData).toHaveBeenCalledWith(
        {
          featureFlags: expect.objectContaining({ calendarModule: true }),
        },
        { startDate: '2025-04-07', endDate: '2025-04-14', locationId: 12, filters: {} },
        expect.any(Object),
      );

      await wrapper.get('button[aria-label="Previous"]').trigger('click');
      await flushPromises();

      const previousRange = shiftDateRange('2025-04-07', 'week', -1);
      expect(loadData).toHaveBeenLastCalledWith(
        {
          featureFlags: expect.objectContaining({ calendarModule: true }),
        },
        { startDate: previousRange.startDate, endDate: previousRange.endDate, locationId: 12, filters: {} },
        expect.any(Object),
      );

      await wrapper.get('button[aria-label="Next"]').trigger('click');
      await flushPromises();

      expect(loadData).toHaveBeenLastCalledWith(
        {
          featureFlags: expect.objectContaining({ calendarModule: true }),
        },
        { startDate: '2025-04-07', endDate: '2025-04-14', locationId: 12, filters: {} },
        expect.any(Object),
      );

      await wrapper.get('button.calendar-toolbar__today-button').trigger('click');
      await flushPromises();

      const todayRange = buildDateRangeForPeriod(getTodayDateOnly(), 'week');
      expect(loadData).toHaveBeenLastCalledWith(
        {
          featureFlags: expect.objectContaining({ calendarModule: true }),
        },
        { startDate: todayRange.startDate, endDate: todayRange.endDate, locationId: 12, filters: {} },
        expect.any(Object),
      );

      await wrapper.get('[data-testid="open-event"]').trigger('click');
      await flushPromises();

      expect(calendarStore.selectedEventId).toBe('evt-1');
      expect(document.body.textContent).toContain('Event Details');
      expect(document.body.textContent).toContain('Main Hall');
    } finally {
      wrapper.unmount();
    }
  });

  it.each([
    [undefined, 'week'],
    [['week', 'day', 'work-week', 'month'] as const, 'month'],
  ] as const)('uses month only when the active view supports it', async (supportedPeriods, expectedPeriod) => {
    const loadData = vi.fn().mockResolvedValue({ contributions: {} });
    const cancel = vi.fn();
    const TestView = defineComponent({ template: '<div data-testid="calendar-view" />' });

    vi.doMock('@/modules/calendar/calendarDataService', () => ({
      calendarDataService: { loadData, cancel },
    }));

    vi.doMock('@/modules/calendar/registry/calendarActionRegistry', () => ({
      calendarActionRegistry: {
        getCreateActions: vi.fn(() => []),
        getToolbarActionsForView: vi.fn(() => []),
        getViewDetailActions: vi.fn(() => []),
      },
    }));

    vi.doMock('@/modules/calendar/registry/calendarRegistry', () => ({
      calendarRegistry: {
        getAvailableViews: vi.fn(() => [
          {
            id: 'period-view',
            label: 'Period View',
            component: TestView,
            supportedPeriods,
            buildModel: () => ({}),
          },
        ]),
      },
    }));

    const [{ default: Calendar }, { useCalendarStore }, { buildDateRangeForPeriod }] = await Promise.all([
      import('@/modules/calendar/Calendar.vue'),
      import('@/modules/calendar/calendarStore'),
      import('@/utils/date'),
    ]);

    const { mountPlugins, pinia } = await createTestApp({ featureFlags: { calendarModule: true } });
    const calendarStore = useCalendarStore(pinia);
    calendarStore.setPeriod('month');
    calendarStore.setDateRange('2025-04-01', '2025-05-01');

    const wrapper = mount(Calendar, {
      attachTo: document.body,
      global: {
        plugins: mountPlugins,
      },
    });

    try {
      await flushPromises();

      const expectedRange = buildDateRangeForPeriod('2025-04-01', expectedPeriod);
      expect(calendarStore.period).toBe(expectedPeriod);
      expect(loadData).toHaveBeenLastCalledWith(
        {
          featureFlags: expect.objectContaining({ calendarModule: true }),
        },
        { startDate: expectedRange.startDate, endDate: expectedRange.endDate, locationId: undefined, filters: {} },
        expect.any(Object),
      );
    } finally {
      wrapper.unmount();
    }
  });
});
