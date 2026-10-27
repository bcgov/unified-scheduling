import { mount, flushPromises } from '@vue/test-utils';
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { defineComponent } from 'vue';
import { DateTime } from 'luxon';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import type { CalendarDataResponse } from '@/modules/calendar/calendarTypes';
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
        getEventDetailComponent: vi.fn(() => undefined),
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
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        { startDate: '2025-04-07', endDate: '2025-04-14', locationId: 12, activeViewId: 'workflow-view', filters: {} },
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
          locationId: 12,
          activeViewId: 'workflow-view',
          filters: {},
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
        { startDate: '2025-04-07', endDate: '2025-04-14', locationId: 12, activeViewId: 'workflow-view', filters: {} },
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
          locationId: 12,
          activeViewId: 'workflow-view',
          filters: {},
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
        { startDate: '2025-04-14', endDate: '2025-04-21', locationId: 12, activeViewId: 'workflow-view', filters: {} },
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
        { startDate: '2025-04-16', endDate: '2025-04-17', locationId: 12, activeViewId: 'workflow-view', filters: {} },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );

      toolbar.vm.$emit('update:period', 'work-week');
      await flushPromises();

      expect(loadData).toHaveBeenLastCalledWith(
        expect.objectContaining({
          featureFlags: expect.objectContaining({ Calendar: expect.objectContaining({ enabled: true }) }),
          permissions: [],
        }),
        { startDate: '2025-04-14', endDate: '2025-04-19', locationId: 12, activeViewId: 'workflow-view', filters: {} },
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
          locationId: 12,
          activeViewId: 'period-view',
          filters: {},
        },
        expect.objectContaining({ getAvailableViews: expect.any(Function) }),
      );
    } finally {
      wrapper.unmount();
    }
  });

  it('reloads on view, location and period changes and ignores stale warning responses', async () => {
    const pending: ((value: CalendarDataResponse) => void)[] = [];
    const loadData = vi.fn(
      () =>
        new Promise<CalendarDataResponse>((resolve) => {
          pending.push(resolve);
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
            component: defineComponent({
              props: ['model'],
              template: '<div data-testid="matrix-view">{{ model.warning }}</div>',
            }),
            buildModel: (data: CalendarDataResponse) => ({
              warning: data.contributions['training.events']?.data,
              days: [{ date: '2025-04-07', label: 'Mon' }],
              primaryColumn: { label: 'TEAM', resources: [] },
              cells: [],
            }),
          },
          {
            id: 'calendar.matrix-assignment',
            label: 'Assignment View',
            loadingVariant: 'matrix',
            component: defineComponent({
              props: ['model'],
              template: '<div data-testid="matrix-view">{{ model.warning }}</div>',
            }),
            buildModel: (data: CalendarDataResponse) => ({ warning: data.contributions['training.events']?.data }),
          },
        ]),
      },
    }));

    const [{ default: Calendar }, { useLocationsStore }, { useCalendarStore }] = await Promise.all([
      import('@/modules/calendar/Calendar.vue'),
      import('@/stores/LocationsStore'),
      import('@/modules/calendar/calendarStore'),
    ]);
    const { mountPlugins, pinia } = await createTestApp({ loadConfig: false });
    useLocationsStore(pinia).setSelectedLocationId(12);
    const wrapper = mount(Calendar, { global: { plugins: mountPlugins } });

    await wrapper.vm.$nextTick();
    expect(wrapper.find('[aria-label="Loading calendar matrix"]').exists()).toBe(true);

    const store = useCalendarStore(pinia);
    store.setActiveView('calendar.matrix-assignment');
    await wrapper.vm.$nextTick();
    expect(loadData).toHaveBeenLastCalledWith(
      expect.anything(),
      expect.objectContaining({ activeViewId: 'calendar.matrix-assignment', locationId: 12 }),
      expect.anything(),
    );

    const response = (warning: string): CalendarDataResponse => ({
      contributions: {
        'training.events': { moduleId: 'training', contributionId: 'training.events', events: [], data: warning },
      },
    });
    pending[1]!(response('current warning'));
    await flushPromises();
    expect(wrapper.get('[data-testid="matrix-view"]').text()).toBe('current warning');
    pending[0]!(response('stale warning'));
    await flushPromises();
    expect(wrapper.get('[data-testid="matrix-view"]').text()).toBe('current warning');

    useLocationsStore(pinia).setSelectedLocationId(13);
    await wrapper.vm.$nextTick();
    expect(wrapper.find('[aria-label="Loading calendar matrix"]').exists()).toBe(true);
    expect(loadData).toHaveBeenLastCalledWith(
      expect.anything(),
      expect.objectContaining({ locationId: 13 }),
      expect.anything(),
    );
    pending[2]!(response('new location warning'));
    await flushPromises();
    expect(wrapper.get('[data-testid="matrix-view"]').text()).toBe('new location warning');

    store.setPeriod('day');
    await wrapper.vm.$nextTick();
    expect(loadData).toHaveBeenCalledTimes(4);
    expect(loadData).toHaveBeenLastCalledWith(
      expect.anything(),
      expect.objectContaining({ startDate: store.dateRange.startDate, endDate: store.dateRange.endDate }),
      expect.anything(),
    );
    pending[3]!(response('new period warning'));
    await flushPromises();
    expect(wrapper.get('[data-testid="matrix-view"]').text()).toBe('new period warning');
    wrapper.unmount();
  });
});
