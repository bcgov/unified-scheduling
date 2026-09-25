import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useCalendarAlertStore } from '@/modules/calendar/calendarAlertStore';

describe('calendarAlertStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('adds and replaces alerts by id without changing their order', () => {
    const store = useCalendarAlertStore();

    store.setAlert({ id: 'calendar.location.required', severity: 'warning', message: 'Please select a location.' });
    store.setAlert({ id: 'scheduling.load.failed', severity: 'error', message: 'Unable to load scheduling data.' });
    store.setAlert({
      id: 'calendar.location.required',
      severity: 'error',
      message: 'Location is still required.',
    });

    expect(store.alerts).toEqual([
      { id: 'calendar.location.required', severity: 'error', message: 'Location is still required.' },
      { id: 'scheduling.load.failed', severity: 'error', message: 'Unable to load scheduling data.' },
    ]);
  });

  it('clears one alert by id', () => {
    const store = useCalendarAlertStore();

    store.setAlert({ id: 'one', severity: 'warning', message: 'One' });
    store.setAlert({ id: 'two', severity: 'error', message: 'Two' });
    store.clearAlert('one');

    expect(store.alerts).toEqual([{ id: 'two', severity: 'error', message: 'Two' }]);
  });
});
