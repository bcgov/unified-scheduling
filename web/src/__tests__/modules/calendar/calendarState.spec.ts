import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { calendarEventDetailModalAction } from '@/modules/calendar/actions/calendarEventDetailModalAction';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { CalendarRegistry } from '@/modules/calendar/registry/calendarRegistry';
import type {
  CalendarModuleContribution,
  CalendarViewDefinition,
  CalendarViewDetailAction,
} from '@/modules/calendar/registry/calendarRegistryTypes';
import type { CalendarCreateAction } from '@/modules/calendar/registry/calendarActionRegistryTypes';
import type { CalendarQueryContext, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';

describe('calendar registries and store', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('registers views, contributions, actions and filters by availability', () => {
    const registry = new CalendarRegistry();
    const runtimeContext: CalendarRuntimeContext = { featureFlags: {} };
    const queryContext: CalendarQueryContext = { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} };

    const slowView: CalendarViewDefinition = {
      id: 'slow',
      label: 'Slow',
      order: 20,
      component: {} as never,
      buildModel: () => ({}),
    };
    const fastView: CalendarViewDefinition = {
      id: 'fast',
      label: 'Fast',
      order: 10,
      component: {} as never,
      buildModel: () => ({}),
      isAvailable: () => true,
      toolbarActions: [{ id: 'refresh', label: 'Refresh' }],
    };
    const hiddenView: CalendarViewDefinition = {
      id: 'hidden',
      label: 'Hidden',
      component: {} as never,
      buildModel: () => ({}),
      isAvailable: () => false,
    };

    registry.registerView(slowView);
    registry.registerView(fastView);
    registry.registerView(hiddenView);

    expect(registry.getAvailableViews(runtimeContext).map((view) => view.id)).toEqual(['fast', 'slow']);
    expect(registry.getToolbarActionsForView('fast')).toEqual([{ id: 'refresh', label: 'Refresh' }]);
    expect(registry.getToolbarActionsForView('missing')).toEqual([]);

    const visibleContribution: CalendarModuleContribution = {
      moduleId: 'calendar',
      contributionId: 'visible',
      load: async () => ({ moduleId: 'calendar', contributionId: 'visible', events: [] }),
    };
    const hiddenContribution: CalendarModuleContribution = {
      moduleId: 'calendar',
      contributionId: 'hidden',
      isAvailable: () => false,
      load: async () => ({ moduleId: 'calendar', contributionId: 'hidden', events: [] }),
    };

    registry.registerModuleContribution(visibleContribution);
    registry.registerModuleContribution(hiddenContribution);

    expect(registry.getAvailableModuleContributions(runtimeContext, queryContext)).toEqual([visibleContribution]);

    const visibleAction: CalendarViewDetailAction = {
      id: 'open',
      moduleId: 'calendar',
      run: () => {},
    };
    const hiddenAction: CalendarViewDetailAction = {
      id: 'hidden',
      moduleId: 'calendar',
      isAvailable: () => false,
      run: () => {},
    };

    registry.registerViewDetailAction('fast', visibleAction);
    registry.registerViewDetailAction('fast', hiddenAction);

    expect(registry.getViewDetailActions('fast')).toEqual([visibleAction, hiddenAction]);
    expect(
      registry.getViewDetailActions('fast', {
        event: { id: '1', type: 'calendar.general', sourceModule: 'calendar', title: 'Event', start: '2025-01-01' },
        viewId: 'fast',
        queryContext,
        runtimeContext,
      }),
    ).toEqual([visibleAction]);
  });

  it('rejects duplicate registry entries and create actions', async () => {
    const { calendarActionRegistry } = await import('@/modules/calendar/registry/calendarActionRegistry');
    const registry = new CalendarRegistry();
    const view: CalendarViewDefinition = {
      id: 'default',
      label: 'Default',
      component: {} as never,
      buildModel: () => ({}),
    };
    registry.registerView(view);
    expect(() => registry.registerView(view)).toThrow("Calendar view 'default' is already registered.");

    const contribution: CalendarModuleContribution = {
      moduleId: 'calendar',
      contributionId: 'calendar.events',
      load: async () => ({ moduleId: 'calendar', contributionId: 'calendar.events', events: [] }),
    };
    registry.registerModuleContribution(contribution);
    expect(() => registry.registerModuleContribution(contribution)).toThrow(
      "Calendar contribution 'calendar.events' is already registered.",
    );

    const detailAction: CalendarViewDetailAction = {
      id: 'open',
      moduleId: 'calendar',
      run: () => {},
    };
    registry.registerViewDetailAction('default', detailAction);
    expect(() => registry.registerViewDetailAction('default', detailAction)).toThrow(
      "Calendar detail action 'open' is already registered for view 'default'.",
    );

    const createAction: CalendarCreateAction = { id: 'create', label: 'Create', moduleId: 'calendar' };
    calendarActionRegistry.registerCreateAction(createAction);
    expect(() => calendarActionRegistry.registerCreateAction(createAction)).toThrow(
      "Calendar create action 'create' is already registered.",
    );
  });

  it('filters create actions and updates the calendar store state', async () => {
    const { calendarActionRegistry } = await import('@/modules/calendar/registry/calendarActionRegistry');
    const runtimeContext: CalendarRuntimeContext = { featureFlags: {} };
    const createContext = { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} };

    const availableAction: CalendarCreateAction = { id: 'available', label: 'Available', moduleId: 'calendar' };
    const hiddenAction: CalendarCreateAction = {
      id: 'hidden',
      label: 'Hidden',
      moduleId: 'calendar',
      isAvailable: () => false,
    };

    const actionRegistryModule = new (class {
      register(action: CalendarCreateAction) {
        calendarActionRegistry.registerCreateAction(action);
      }
    })();

    actionRegistryModule.register(availableAction);
    actionRegistryModule.register(hiddenAction);

    expect(calendarActionRegistry.getCreateActions(createContext, runtimeContext)).toEqual([availableAction]);

    const store = useCalendarStore();
    store.setActiveView('calendar-default');
    store.setDateRange('2025-02-01', '2025-02-08');
    store.setPeriod('month');
    store.setLocationId('42');
    store.setFilter('owner', 'me');
    store.setSelectedEvent('event-1');
    store.setSelectedResource('resource-1');

    expect(store.activeViewId).toBe('calendar-default');
    expect(store.dateRange).toEqual({ startDate: '2025-02-01', endDate: '2025-02-08' });
    expect(store.period).toBe('month');
    expect(store.locationId).toBe('42');
    expect(store.filters).toEqual({ owner: 'me' });
    expect(store.selectedEventId).toBe('event-1');
    expect(store.selectedResourceId).toBe('resource-1');

    store.clearFilter('owner');
    store.clearSelectedEvent();
    store.setLocationId('');
    store.clearSelection();

    expect(store.filters).toEqual({});
    expect(store.selectedEventId).toBeUndefined();
    expect(store.selectedResourceId).toBeUndefined();
    expect(store.locationId).toBeUndefined();
  });

  it('opens the event detail modal by selecting the event in the store', () => {
    const store = useCalendarStore();

    calendarEventDetailModalAction.run({
      event: {
        id: 'event-99',
        type: 'calendar.general',
        sourceModule: 'calendar',
        title: 'Event',
        start: '2025-01-01',
      },
      viewId: 'calendar-default',
      queryContext: { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} },
      runtimeContext: { featureFlags: {} },
    });

    expect(store.selectedEventId).toBe('event-99');
  });
});
