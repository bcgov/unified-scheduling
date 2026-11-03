import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

const TestView = defineComponent({
  name: 'CalendarAlertTestView',
  emits: ['eventClick'],
  template: '<div data-testid="calendar-alert-test-view">Calendar view</div>',
});

async function mountCalendarWithLocation(selectedLocationId: number | '', timeZone?: string) {
  const loadData = vi.fn().mockResolvedValue({ contributions: {} });
  const cancel = vi.fn();
  const buildModel = vi.fn(() => ({ ready: true }));
  const runAction = vi.fn();

  vi.doMock('@/modules/calendar/calendarDataService', () => ({
    calendarDataService: {
      loadData,
      cancel,
      endSession: vi.fn(),
    },
  }));
  vi.doMock('@/modules/calendar/registry/calendarRegistry', () => ({
    calendarRegistry: {
      getAvailableViews: vi.fn(() => [
        {
          id: 'alert-test-view',
          label: 'Alerts',
          component: TestView,
          buildModel,
        },
      ]),
    },
  }));
  vi.doMock('@/modules/calendar/registry/calendarActionRegistry', () => ({
    calendarActionRegistry: {
      getToolbarActionsForView: vi.fn(() => [{ id: 'manage', label: 'Manage', run: runAction }]),
      getCreateActions: vi.fn(() => [{ id: 'create', label: 'Create', run: runAction }]),
      getViewDetailActions: vi.fn(() => [{ id: 'detail', run: runAction }]),
    },
  }));

  const [{ default: Calendar }, { useLocationsStore }, { useCalendarAlertStore }] = await Promise.all([
    import('@/modules/calendar/Calendar.vue'),
    import('@/stores/LocationsStore'),
    import('@/modules/calendar/calendarAlertStore'),
  ]);
  const app = await createTestApp({ featureFlags: { Calendar: { enabled: true } } });
  const locationsStore = useLocationsStore(app.pinia);
  const calendarAlertStore = useCalendarAlertStore(app.pinia);

  locationsStore.entities = [{ id: 12, name: 'Main Hall', timezone: timeZone }] as never[];
  locationsStore.setSelectedLocationId(selectedLocationId);

  const wrapper = mount(Calendar, {
    attachTo: document.body,
    global: {
      plugins: app.mountPlugins,
    },
  });

  await flushPromises();

  return {
    wrapper,
    loadData,
    cancel,
    buildModel,
    runAction,
    locationsStore,
    calendarAlertStore,
  };
}

describe('Calendar alerts', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('renders no central location alert when an active location is selected', async () => {
    const { wrapper } = await mountCalendarWithLocation(12);

    expect(document.body.textContent).not.toContain('Please select a location.');

    wrapper.unmount();
  });

  it.each(['America/Vancouver', 'America/Edmonton', 'America/Dawson_Creek', 'America/Creston'])(
    'blocks invalid location zones with a recoverable alert and resumes with %s',
    async (validTimeZone) => {
      const { wrapper, loadData, cancel, buildModel, runAction, locationsStore, calendarAlertStore } =
        await mountCalendarWithLocation(12, 'Invalid/ConfiguredZone');
      try {
        const { default: CalendarToolbar } = await import('@/modules/calendar/components/CalendarToolbar.vue');
        const toolbar = wrapper.findComponent(CalendarToolbar);
        const event = {
          id: 'event',
          type: 'calendar.general',
          sourceModule: 'calendar',
          title: 'Event',
          start: '2026-07-13',
        };
        expect(document.body.textContent).toContain('The selected location has an invalid time zone.');
        expect(calendarAlertStore.alerts).toContainEqual(
          expect.objectContaining({ severity: 'error', dismissible: false }),
        );
        expect(wrapper.findComponent(TestView).exists()).toBe(false);
        expect(loadData).not.toHaveBeenCalled();
        expect(buildModel).not.toHaveBeenCalled();
        expect(toolbar.props('createActions')).toEqual([]);
        expect(toolbar.props('toolbarActions')).toEqual([]);
        toolbar.vm.$emit('today');
        toolbar.vm.$emit('trigger-create-action', 'create');
        toolbar.vm.$emit('trigger-toolbar-action', 'manage');
        await (
          wrapper.vm as unknown as { handleViewEventClick: (value: typeof event) => Promise<void> }
        ).handleViewEventClick(event);
        await flushPromises();
        expect(runAction).not.toHaveBeenCalled();

        locationsStore.entities = [{ id: 12, name: 'Main Hall', timezone: validTimeZone }];
        await flushPromises();
        expect(document.body.textContent).not.toContain('The selected location has an invalid time zone.');
        expect(calendarAlertStore.alerts).toEqual([]);
        expect(wrapper.findComponent(TestView).exists()).toBe(true);
        expect(loadData).toHaveBeenCalledOnce();
        toolbar.vm.$emit('trigger-create-action', 'create');
        toolbar.vm.$emit('trigger-toolbar-action', 'manage');
        wrapper.findComponent(TestView).vm.$emit('eventClick', event);
        await flushPromises();
        expect(runAction).toHaveBeenCalledTimes(3);

        locationsStore.entities = [{ id: 12, name: 'Main Hall', timezone: 'Invalid/ConfiguredZone' }];
        await flushPromises();
        expect(wrapper.findComponent(TestView).exists()).toBe(false);
        expect(document.body.textContent).toContain('The selected location has an invalid time zone.');
        expect(loadData).toHaveBeenCalledOnce();
        expect(cancel).toHaveBeenCalledTimes(2);
      } finally {
        wrapper.unmount();
      }
    },
  );

  it('renders a warning when no active location is selected', async () => {
    const { wrapper, loadData, locationsStore } = await mountCalendarWithLocation('');

    expect(document.body.textContent).toContain('Please select a location.');
    expect(wrapper.find('[data-testid="calendar-alert-test-view"]').exists()).toBe(false);
    expect(loadData).not.toHaveBeenCalled();

    locationsStore.setSelectedLocationId(12);
    await flushPromises();

    expect(wrapper.find('[data-testid="calendar-alert-test-view"]').exists()).toBe(true);
    expect(loadData).toHaveBeenCalledOnce();

    wrapper.unmount();
  });

  it('dismisses the location warning from the central alert bar', async () => {
    const { wrapper, calendarAlertStore } = await mountCalendarWithLocation('');

    expect(document.body.textContent).toContain('Please select a location.');

    const closeButton = wrapper.get('.calendar-alerts .v-alert__close button');
    await closeButton.trigger('click');
    await flushPromises();

    expect(calendarAlertStore.alerts).toEqual([]);
    expect(document.body.textContent).not.toContain('Please select a location.');

    wrapper.unmount();
  });

  it('clears the location warning when a location is selected', async () => {
    const { wrapper, locationsStore } = await mountCalendarWithLocation('');

    expect(document.body.textContent).toContain('Please select a location.');

    locationsStore.setSelectedLocationId(12);
    await flushPromises();

    expect(document.body.textContent).not.toContain('Please select a location.');

    wrapper.unmount();
  });

  it('shows the location warning when a selected location is cleared', async () => {
    const { wrapper, locationsStore } = await mountCalendarWithLocation(12);

    expect(document.body.textContent).not.toContain('Please select a location.');

    locationsStore.setSelectedLocationId('');
    await flushPromises();

    expect(document.body.textContent).toContain('Please select a location.');

    wrapper.unmount();
  });

  it('renders module alerts contributed through the calendar alert store in stable order', async () => {
    const { wrapper, calendarAlertStore } = await mountCalendarWithLocation(12);

    calendarAlertStore.setAlert({
      id: 'scheduling.load.failed',
      severity: 'error',
      message: 'Unable to load scheduling assignments.',
      source: 'scheduling',
    });
    calendarAlertStore.setAlert({
      id: 'training.load.failed',
      severity: 'warning',
      message: 'Unable to load training events.',
      source: 'training',
    });
    await flushPromises();

    const alertMessages = wrapper.findAll('.calendar-alerts [role="alert"]').map((alert) => alert.text());
    expect(alertMessages).toEqual(['Unable to load scheduling assignments.', 'Unable to load training events.']);

    wrapper.unmount();
  });
});
