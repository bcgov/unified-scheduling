import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

const TestView = defineComponent({
  name: 'CalendarAlertTestView',
  template: '<div data-testid="calendar-alert-test-view">Calendar view</div>',
});

async function mountCalendarWithLocation(selectedLocationId: number | '') {
  const loadData = vi.fn().mockResolvedValue({ contributions: {} });

  vi.doMock('@/modules/calendar/calendarDataService', () => ({
    calendarDataService: {
      loadData,
      cancel: vi.fn(),
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
          buildModel: () => ({ ready: true }),
        },
      ]),
    },
  }));
  vi.doMock('@/modules/calendar/registry/calendarActionRegistry', () => ({
    calendarActionRegistry: {
      getToolbarActionsForView: vi.fn(() => []),
      getCreateActions: vi.fn(() => []),
      getViewDetailActions: vi.fn(() => []),
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

  locationsStore.entities = [{ id: 12, name: 'Main Hall' }] as never[];
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

    const alertMessages = Array.from(document.querySelectorAll('[role="alert"]')).map((alert) => alert.textContent);
    expect(alertMessages).toEqual(['Unable to load scheduling assignments.', 'Unable to load training events.']);

    wrapper.unmount();
  });
});
