import { mount, flushPromises } from '@vue/test-utils';
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { defineComponent } from 'vue';
import { DateTime } from 'luxon';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import type { CalendarFeatureFlags } from '@/api-access/generated/models';

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
    const endSession = vi.fn();

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
      calendarDataService: { loadData, cancel, endSession },
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
      { buildDateRangeForPeriod, formatRangeLabel, getTodayDateOnly, shiftCalendarAnchor },
    ] = await Promise.all([
      import('@/modules/calendar/Calendar.vue'),
      import('@/modules/calendar/calendarStore'),
      import('@/stores/LocationsStore'),
      import('@/utils/date'),
    ]);

    const calendarFeatureFlags: CalendarFeatureFlags = {
      source: 'Calendar',
      enabled: true,
      calendarMatrixTest: false,
    };
    const { mountPlugins, pinia } = await createTestApp({ featureFlags: { Calendar: calendarFeatureFlags } });
    const calendarStore = useCalendarStore(pinia);
    const selectEventById = (eventId: string) => calendarStore.setSelectedEvent(eventId);
    const locationsStore = useLocationsStore(pinia);

    calendarStore.setPeriod('week');
    calendarStore.setAnchorDate('2025-04-07');
    calendarStore.setFilter('timeZoneId', 'America/Vancouver');
    locationsStore.entities = [{ id: 12, name: 'Main Hall', timezone: 'America/Toronto' }];
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
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: '2025-04-07',
          endDate: '2025-04-14',
          activeViewId: 'workflow-view',
          locationId: 12,
          filters: { timeZoneId: 'America/Toronto' },
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );

      await wrapper.get('button[aria-label="Previous"]').trigger('click');
      await flushPromises();

      const previousAnchor = shiftCalendarAnchor('2025-04-07', 'week', 'previous');
      const previousRange = buildDateRangeForPeriod(previousAnchor, 'week');
      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: previousRange.startDate,
          endDate: previousRange.endDate,
          activeViewId: 'workflow-view',
          locationId: 12,
          filters: { timeZoneId: 'America/Toronto' },
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );

      await wrapper.get('button[aria-label="Next"]').trigger('click');
      await flushPromises();

      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: '2025-04-07',
          endDate: '2025-04-14',
          activeViewId: 'workflow-view',
          locationId: 12,
          filters: { timeZoneId: 'America/Toronto' },
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );

      await wrapper.get('button.calendar-toolbar__today-button').trigger('click');
      await flushPromises();

      const todayRange = buildDateRangeForPeriod(getTodayDateOnly(), 'week');
      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: todayRange.startDate,
          endDate: todayRange.endDate,
          activeViewId: 'workflow-view',
          locationId: 12,
          filters: { timeZoneId: 'America/Toronto' },
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );

      const rangeButton = wrapper.get('button.calendar-toolbar__range');
      expect(rangeButton.attributes('aria-label')).toContain('Choose date');

      await rangeButton.trigger('click');
      await flushPromises();

      const datePicker = wrapper.findComponent({ name: 'VDatePicker' });
      expect(datePicker.exists()).toBe(true);

      datePicker.vm.$emit('update:modelValue', DateTime.fromISO('2025-04-16'));
      await flushPromises();

      expect(calendarStore.anchorDate).toBe('2025-04-16');
      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: '2025-04-14',
          endDate: '2025-04-21',
          activeViewId: 'workflow-view',
          locationId: 12,
          filters: { timeZoneId: 'America/Toronto' },
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );

      const toolbar = wrapper.findComponent({ name: 'CalendarToolbar' });
      toolbar.vm.$emit('update:period', 'day');
      await flushPromises();

      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: '2025-04-16',
          endDate: '2025-04-17',
          activeViewId: 'workflow-view',
          locationId: 12,
          filters: { timeZoneId: 'America/Toronto' },
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );

      toolbar.vm.$emit('update:period', 'work-week');
      await flushPromises();

      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: '2025-04-14',
          endDate: '2025-04-19',
          activeViewId: 'workflow-view',
          locationId: 12,
          filters: { timeZoneId: 'America/Toronto' },
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
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
    const endSession = vi.fn();
    const TestView = defineComponent({ template: '<div data-testid="calendar-view" />' });

    vi.doMock('@/modules/calendar/calendarDataService', () => ({
      calendarDataService: { loadData, cancel, endSession },
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

    const [{ default: Calendar }, { useCalendarStore }, { useLocationsStore }, { buildDateRangeForPeriod }] =
      await Promise.all([
        import('@/modules/calendar/Calendar.vue'),
        import('@/modules/calendar/calendarStore'),
        import('@/stores/LocationsStore'),
        import('@/utils/date'),
      ]);

    const calendarFeatureFlags: CalendarFeatureFlags = {
      source: 'Calendar',
      enabled: true,
      calendarMatrixTest: false,
    };
    const { mountPlugins, pinia } = await createTestApp({ featureFlags: { Calendar: calendarFeatureFlags } });
    const calendarStore = useCalendarStore(pinia);
    const locationsStore = useLocationsStore(pinia);
    calendarStore.setPeriod('month');
    calendarStore.setAnchorDate('2025-04-01');
    locationsStore.setSelectedLocationId(12);

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
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        {
          startDate: expectedRange.startDate,
          endDate: expectedRange.endDate,
          activeViewId: 'period-view',
          locationId: 12,
          filters: {},
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );
    } finally {
      wrapper.unmount();
    }
  });

  it('renders a matrix skeleton while a matrix view is loading', async () => {
    let resolveLoad: (value: { contributions: Record<string, never> }) => void = () => undefined;
    const loadData = vi.fn(
      () =>
        new Promise<{ contributions: Record<string, never> }>((resolve) => {
          resolveLoad = resolve;
        }),
    );

    vi.doMock('@/modules/calendar/calendarDataService', () => ({
      calendarDataService: { loadData, cancel: vi.fn(), endSession: vi.fn() },
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
            id: 'matrix-view',
            label: 'Matrix View',
            loadingVariant: 'matrix',
            component: defineComponent({ template: '<div data-testid="matrix-view" />' }),
            buildModel: () => ({
              days: [{ date: '2025-04-07', label: 'Mon' }],
              primaryColumn: { label: 'TEAM', resources: [] },
              cells: [],
            }),
          },
        ]),
      },
    }));

    const [{ default: Calendar }, { useLocationsStore }] = await Promise.all([
      import('@/modules/calendar/Calendar.vue'),
      import('@/stores/LocationsStore'),
    ]);
    const { mountPlugins, pinia } = await createTestApp({ loadConfig: false });
    useLocationsStore(pinia).setSelectedLocationId(12);
    const wrapper = mount(Calendar, { global: { plugins: mountPlugins } });

    await wrapper.vm.$nextTick();
    expect(wrapper.find('[aria-label="Loading calendar matrix"]').exists()).toBe(true);

    resolveLoad({ contributions: {} });
    await flushPromises();
    expect(wrapper.find('[aria-label="Loading calendar matrix"]').exists()).toBe(false);

    wrapper.unmount();
  });

  it('resolves print context only after current data and child rendering, and reports load failures', async () => {
    const loadData = vi.fn().mockResolvedValue({ contributions: {} });
    const PrintView = defineComponent({
      props: { renderToken: { type: String, default: '' } },
      emits: ['renderComplete'],
      template: `
        <div data-testid="print-view">
          <span>Published schedule content</span>
          <button data-testid="complete-render" @click="$emit('renderComplete', renderToken)">Complete</button>
        </div>
      `,
    });

    vi.doMock('@/modules/calendar/calendarDataService', () => ({
      calendarDataService: { loadData, cancel: vi.fn(), endSession: vi.fn() },
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
            id: 'calendar.matrix-schedule',
            label: 'Schedule',
            component: PrintView,
            supportedPeriods: ['day', 'week', 'work-week'],
            buildModel: () => ({ timeZone: 'America/Vancouver' }),
          },
        ]),
      },
    }));

    const [{ default: Calendar }, { useCalendarStore }, { useLocationsStore }] = await Promise.all([
      import('@/modules/calendar/Calendar.vue'),
      import('@/modules/calendar/calendarStore'),
      import('@/stores/LocationsStore'),
    ]);
    const { mountPlugins, pinia, router } = await createTestApp({ loadConfig: false });
    await router.push({
      path: '/calendar',
      query: {
        mode: 'print',
        view: 'schedule',
        locationId: '7',
        startDate: '2026-08-24',
        endDate: '2026-08-31',
        period: 'week',
      },
    });
    await router.isReady();
    const locationsStore = useLocationsStore(pinia);
    locationsStore.entities = [{ id: 7, name: 'Seven', timezone: 'America/Vancouver' }];
    const calendarStore = useCalendarStore(pinia);
    const wrapper = mount(Calendar, { global: { plugins: mountPlugins } });

    try {
      await flushPromises();

      expect(calendarStore.activeViewId).toBe('calendar.matrix-schedule');
      expect(calendarStore.period).toBe('week');
      expect(calendarStore.dateRange).toEqual({ startDate: '2026-08-24', endDate: '2026-08-31' });
      expect(locationsStore.selectedLocationId).toBe(7);
      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({ presentationMode: 'print' }),
        expect.objectContaining({
          startDate: '2026-08-24',
          endDate: '2026-08-31',
          activeViewId: 'calendar.matrix-schedule',
          locationId: 7,
        }),
        expect.any(Object),
      );
      expect(wrapper.get('#calendar-print-root').attributes('data-render-state')).toBe('loading');

      const printView = wrapper.getComponent(PrintView);
      const firstRenderToken = printView.props('renderToken') as string;
      printView.vm.$emit('renderComplete', 'stale-token');
      await wrapper.vm.$nextTick();
      expect(wrapper.get('#calendar-print-root').attributes('data-render-state')).toBe('loading');

      await wrapper.get('[data-testid="complete-render"]').trigger('click');
      await wrapper.vm.$nextTick();
      expect(wrapper.get('#calendar-print-root').attributes('data-render-state')).toBe('complete');

      loadData.mockRejectedValueOnce(new Error('load failed'));
      calendarStore.refresh();
      await flushPromises();
      expect(wrapper.get('#calendar-print-root').attributes('data-render-state')).toBe('error');
      expect(wrapper.text()).toContain('Failed to retrieve calendar data.');

      printView.vm.$emit('renderComplete', firstRenderToken);
      await wrapper.vm.$nextTick();
      expect(wrapper.get('#calendar-print-root').attributes('data-render-state')).toBe('error');
    } finally {
      wrapper.unmount();
    }
  });
});
