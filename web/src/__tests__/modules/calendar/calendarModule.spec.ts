import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteRecordRaw } from 'vue-router';

describe('calendar module integration', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('registers routes, navigation and calendar contributions only once', async () => {
    const routes: RouteRecordRaw[] = [];

    const [{ registerModule }, { useNavigationStore }, { calendarRegistry }, { calendarActionRegistry }] =
      await Promise.all([
        import('@/modules/calendar/CalendarModule'),
        import('@/stores/NavigationStore'),
        import('@/modules/calendar/registry/calendarRegistry'),
        import('@/modules/calendar/registry/calendarActionRegistry'),
      ]);

    registerModule(routes);
    registerModule(routes);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(1);
    expect(routes[0]?.path).toBe('/calendar');
    expect(navigationStore.links).toEqual([{ name: 'Calendar', path: '/calendar', class: 'router-link--border' }]);
    expect(calendarRegistry.getAvailableViews({ featureFlags: {} }).map((view) => view.id)).toEqual([
      'calendar-default',
    ]);
    expect(
      calendarRegistry
        .getAvailableModuleContributions(
          { featureFlags: { calendarModule: true } },
          { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} },
        )
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
        { startDate: '2025-01-01', endDate: '2025-01-02', filters: {} },
        { featureFlags: {} },
      ),
    ).toHaveLength(1);
  });

  it('loads contribution data and respects the feature flag gate', async () => {
    const postApiCalendarData = vi.fn().mockResolvedValue({
      moduleId: 'calendar',
      contributionId: 'calendar.events',
      events: [
        {
          id: 7,
          title: 'Holiday',
          startAtUtc: '2025-07-01T00:00:00Z',
          endAtUtc: '2025-07-02T00:00:00Z',
          allDay: true,
          isException: false,
          eventTypeCode: 'holiday',
          statusTypeCode: 'active',
          sourceModule: 'calendar',
          locationId: 12,
        },
      ],
    });

    vi.doMock('@/api-access/calendar', () => ({
      postApiCalendarData,
    }));

    const { calendarEventsContribution } = await import('@/modules/calendar/contributions/calendarEventsContribution');

    const isAvailable = calendarEventsContribution.isAvailable;

    expect(isAvailable).toBeTypeOf('function');

    expect(isAvailable?.({ featureFlags: {} }, { startDate: '', endDate: '', filters: {} })).toBe(true);
    expect(
      isAvailable?.({ featureFlags: { calendarModule: false } }, { startDate: '', endDate: '', filters: {} }),
    ).toBe(false);

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
      events: [
        expect.objectContaining({
          id: '7',
          start: '2025-07-01',
          end: '2025-07-02',
          locationId: 12,
        }),
      ],
    });

    expect(postApiCalendarData).toHaveBeenCalledWith(
      {
        startDate: '2025-07-01',
        endDate: '2025-07-08',
        locationId: 12,
        filters: { owner: 'team' },
      },
      { fetchOptions: { signal: expect.any(AbortSignal) } },
    );
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
});
