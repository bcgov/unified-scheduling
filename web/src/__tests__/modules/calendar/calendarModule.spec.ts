import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteRecordRaw } from 'vue-router';
import { server } from '@/__tests__/mocks/server';
import { getPostApiCalendarEventsMockHandler } from '@/api-access/generated/calendar/calendar.msw';
import type { CalendarFeatureFlags, FeatureFlagsResponse } from '@/api-access/generated/models';
import type { CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';

describe('calendar module integration', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('registers routes, navigation and calendar contributions only once when enabled', async () => {
    const routes: RouteRecordRaw[] = [];
    const calendarFeatureFlags: CalendarFeatureFlags = {
      source: 'Calendar',
      enabled: true,
      calendarMatrixTest: false,
    };
    const featureFlags: FeatureFlagsResponse = {
      Calendar: calendarFeatureFlags,
    };

    const [{ registerModule }, { useNavigationStore }, { calendarRegistry }, { calendarActionRegistry }] =
      await Promise.all([
        import('@/modules/calendar/CalendarModule'),
        import('@/stores/NavigationStore'),
        import('@/modules/calendar/registry/calendarRegistry'),
        import('@/modules/calendar/registry/calendarActionRegistry'),
      ]);

    registerModule(routes, featureFlags);
    registerModule(routes, featureFlags);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(1);
    expect(routes[0]?.path).toBe('/calendar');
    expect(navigationStore.links).toEqual([{ name: 'Calendar', path: '/calendar', class: 'router-link--border' }]);
    const runtimeContextEnabled = {
      featureFlags: {
        Calendar: calendarFeatureFlags,
      },
    } as unknown as CalendarRuntimeContext;

    expect(calendarRegistry.getAvailableViews({ featureFlags: {} }).map((view) => view.id)).toEqual([
      'calendar-default',
    ]);
    expect(
      calendarRegistry
        .getAvailableModuleContributions(runtimeContextEnabled, {
          startDate: '2025-01-01',
          endDate: '2025-01-02',
          filters: {},
        })
        .map((contribution) => contribution.contributionId),
    ).toEqual(['calendar.events']);
    expect(
      calendarActionRegistry.getViewDetailActions('calendar-default', {
        event: { id: '1', type: 'calendar.general', sourceModule: 'calendar', title: 'Event', start: '2025-01-01' },
        viewId: 'calendar-default',
        queryContext: { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} },
        runtimeContext: { featureFlags: {} },
      }),
    ).toHaveLength(1);
    expect(
      calendarActionRegistry.getCreateActions(
        { startDate: '2025-01-01', endDate: '2025-01-02', activeViewId: 'calendar-default', filters: {} },
        { featureFlags: {} },
      ),
    ).toHaveLength(1);
  });

  it('does not register routes when calendar feature is disabled', async () => {
    const routes: RouteRecordRaw[] = [];
    const calendarFeatureFlags: CalendarFeatureFlags = {
      source: 'Calendar',
      enabled: false,
      calendarMatrixTest: false,
    };
    const featureFlags: FeatureFlagsResponse = {
      Calendar: calendarFeatureFlags,
    };

    const [{ registerModule }, { useNavigationStore }] = await Promise.all([
      import('@/modules/calendar/CalendarModule'),
      import('@/stores/NavigationStore'),
    ]);

    registerModule(routes, featureFlags);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(0);
    expect(navigationStore.links).toHaveLength(0);
  });

  it('loads contribution data and respects the feature flag gate', async () => {
    let requestBody: unknown;

    server.use(
      getPostApiCalendarEventsMockHandler(async ({ request }) => {
        requestBody = await request.json();

        return {
          moduleId: 'calendar',
          contributionId: 'calendar.events',
          events: [
            {
              id: 'stat-holiday:CanadaDay:2025-07-01',
              title: 'Holiday',
              startAtUtc: '2025-07-01T00:00:00Z',
              endAtUtc: '2025-07-02T00:00:00Z',
              allDay: true,
              isReadOnly: true,
              isException: false,
              holidayType: 'CanadaDay',
              eventTypeCode: 'Holiday',
              statusTypeCode: 'Active',
              sourceModule: 'calendar',
              locationId: 12,
            },
          ],
        };
      }),
    );
    const { calendarEventsContribution } = await import('@/modules/calendar/contributions/calendarEventsContribution');

    const isAvailable = calendarEventsContribution.isAvailable;
    const calendarFeatureFlags: CalendarFeatureFlags = {
      source: 'Calendar',
      enabled: false,
      calendarMatrixTest: false,
    };
    const runtimeContextDisabled = {
      featureFlags: {
        Calendar: calendarFeatureFlags,
      },
    } as unknown as CalendarRuntimeContext;

    expect(isAvailable).toBeTypeOf('function');

    expect(isAvailable?.({ featureFlags: {} }, { startDate: '', endDate: '', filters: {} })).toBe(true);
    expect(isAvailable?.(runtimeContextDisabled, { startDate: '', endDate: '', filters: {} })).toBe(false);

    await expect(
      calendarEventsContribution.load(
        {
          startDate: '2025-07-01',
          endDate: '2025-07-08',
          locationId: 12,
          filters: { owner: 'team' },
        },
        { signal: new AbortController().signal },
      ),
    ).resolves.toEqual({
      moduleId: 'calendar',
      contributionId: 'calendar.events',
      data: { conflicts: [] },
      events: [
        expect.objectContaining({
          id: 'stat-holiday:CanadaDay:2025-07-01',
          start: '2025-07-01',
          end: '2025-07-02',
          isReadOnly: true,
          holidayType: 'CanadaDay',
          locationId: 12,
        }),
      ],
    });

    expect(requestBody).toEqual({
      startDate: '2025-07-01',
      endDate: '2025-07-07',
      locationId: 12,
      filters: { owner: 'team' },
    });
  });

  it('aborts stale calendar data requests and supports manual cancellation', async () => {
    const pendingSignals: AbortSignal[] = [];
    let resolveFirstLoad: (() => void) | undefined;

    const firstContribution = {
      contributionId: 'first',
      load: vi.fn(
        (_context, options?: { signal?: AbortSignal }) =>
          new Promise<{ moduleId: string; contributionId: string; events: [] }>((resolve) => {
            pendingSignals.push(options?.signal as AbortSignal);
            resolveFirstLoad = () => resolve({ moduleId: 'calendar', contributionId: 'first', events: [] });
          }),
      ),
    };
    const secondContribution = {
      contributionId: 'second',
      load: vi.fn(async () => ({ moduleId: 'calendar', contributionId: 'second', events: [] as [] })),
    };

    const registry = {
      getAvailableModuleContributions: vi
        .fn()
        .mockReturnValueOnce([firstContribution])
        .mockReturnValueOnce([secondContribution]),
    };

    const { calendarDataService } = await import('@/modules/calendar/calendarDataService');

    const firstRequest = calendarDataService.loadData(
      { featureFlags: {} },
      { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} },
      registry,
    );

    const secondRequest = calendarDataService.loadData(
      { featureFlags: {} },
      { startDate: '2025-01-08', endDate: '2025-01-09', filters: {} },
      registry,
    );

    resolveFirstLoad?.();

    await expect(firstRequest).rejects.toMatchObject({ name: 'AbortError' });
    await expect(secondRequest).resolves.toEqual({
      contributions: {
        second: {
          moduleId: 'calendar',
          contributionId: 'second',
          events: [],
        },
      },
    });

    expect(pendingSignals[0]?.aborted).toBe(true);

    const neverResolvesRegistry = {
      getAvailableModuleContributions: vi.fn().mockReturnValue([
        {
          contributionId: 'pending',
          load: vi.fn(() => new Promise(() => {})),
        },
      ]),
    };

    void calendarDataService.loadData(
      { featureFlags: {} },
      { startDate: '2025-02-01', endDate: '2025-02-02', filters: {} },
      neverResolvesRegistry,
    );

    calendarDataService.cancel();

    const finalRegistry = {
      getAvailableModuleContributions: vi.fn().mockReturnValue([
        {
          contributionId: 'final',
          load: vi.fn(async (_context, options?: { signal?: AbortSignal }) => {
            expect(options?.signal?.aborted).toBe(false);
            return { moduleId: 'calendar', contributionId: 'final', events: [] as [] };
          }),
        },
      ]),
    };

    await expect(
      calendarDataService.loadData(
        { featureFlags: {} },
        { startDate: '2025-03-01', endDate: '2025-03-02', filters: {} },
        finalRegistry,
      ),
    ).resolves.toEqual({
      contributions: {
        final: {
          moduleId: 'calendar',
          contributionId: 'final',
          events: [],
        },
      },
    });
  });

  it('deactivates loaded contributions once when the calendar session ends', async () => {
    const onDeactivate = vi.fn();
    const contribution = {
      moduleId: 'calendar',
      contributionId: 'session-contribution',
      onDeactivate,
      load: vi.fn(async () => ({
        moduleId: 'calendar',
        contributionId: 'session-contribution',
        events: [] as [],
      })),
    };
    const registry = {
      getAvailableModuleContributions: vi.fn(() => [contribution]),
    };
    const { calendarDataService } = await import('@/modules/calendar/calendarDataService');
    const runtimeContext = { featureFlags: {} };
    const queryContext = { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} };

    await calendarDataService.loadData(runtimeContext, queryContext, registry);
    await calendarDataService.loadData(runtimeContext, queryContext, registry);

    calendarDataService.endSession();
    calendarDataService.endSession();

    expect(onDeactivate).toHaveBeenCalledOnce();
  });
});
