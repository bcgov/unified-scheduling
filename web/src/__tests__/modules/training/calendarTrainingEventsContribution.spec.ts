import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { TrainingCalendarEventsData } from '@/modules/training/contributions/calendarTrainingEventsContribution';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import { calendarTrainingEventsContribution } from '@/modules/training/contributions/calendarTrainingEventsContribution';
import { CalendarRegistry } from '@/modules/calendar/registry/calendarRegistry';
import { calendarDataService } from '@/modules/calendar/calendarDataService';

const userId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const otherUserId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const context = {
  startDate: '2025-04-07',
  endDate: '2025-04-14',
  locationId: 12,
  filters: { userIds: [userId, '', 'not-a-guid', otherUserId] },
};

describe('calendarTrainingEventsContribution', () => {
  beforeEach(() => setActivePinia(createPinia()));

  it('gates loading on both modules and UserTrainingsView', () => {
    const flags = {
      Calendar: { source: 'Calendar', enabled: true, calendarMatrixTest: false },
      Training: { source: 'Training', enabled: true },
    };
    const available = calendarTrainingEventsContribution.isAvailable!;
    expect(available({ featureFlags: flags, permissions: [Permissions.UserTrainingsView] }, context)).toBe(true);
    expect(available({ featureFlags: flags, permissions: [] }, context)).toBe(false);
    expect(
      available(
        {
          featureFlags: { ...flags, Training: { ...flags.Training, enabled: false } },
          permissions: [Permissions.UserTrainingsView],
        },
        context,
      ),
    ).toBe(false);
    expect(
      available(
        {
          featureFlags: { ...flags, Calendar: { ...flags.Calendar, enabled: false } },
          permissions: [Permissions.UserTrainingsView],
        },
        context,
      ),
    ).toBe(false);
  });

  it('sends inclusive dates and only valid IDs from a mixed filter, then maps read-only all-day events', async () => {
    let body: unknown;
    let requests = 0;
    server.use(
      http.post('*/api/training/calendar/events', async ({ request }) => {
        requests++;
        body = await request.json();
        return HttpResponse.json([
          {
            id: 'training.user-training.42',
            userTrainingId: 42,
            userId,
            trainingId: 7,
            type: 'training.user-training',
            sourceModule: 'training',
            title: 'Training: FIRST-AID',
            start: '2025-04-08T00:00:00Z',
            end: '2025-04-11T00:00:00Z',
            allDay: true,
            isReadOnly: true,
            locationId: 12,
          },
        ]);
      }),
    );

    const signal = new AbortController().signal;
    const result = await calendarTrainingEventsContribution.load(context, { signal });

    expect(requests).toBe(1);
    expect(body).toEqual({
      startDate: '2025-04-07',
      endDate: '2025-04-13',
      locationId: 12,
      userIds: [userId, otherUserId],
    });
    expect(result.events).toEqual([
      expect.objectContaining({
        id: 'training.user-training.42',
        title: 'Training: FIRST-AID',
        type: 'training.user-training',
        start: '2025-04-08',
        end: '2025-04-11',
        allDay: true,
        isReadOnly: true,
        locationId: 12,
        resourceIds: [userId],
      }),
    ]);
  });

  it.each([
    ['absent', {}],
    ['empty', { userIds: [] }],
  ])('omits the user filter when it is %s', async (_case, filters) => {
    let body: unknown;
    let requests = 0;
    server.use(
      http.post('*/api/training/calendar/events', async ({ request }) => {
        requests++;
        body = await request.json();
        return HttpResponse.json([]);
      }),
    );

    await calendarTrainingEventsContribution.load({ ...context, filters });

    expect(requests).toBe(1);
    expect(body).toEqual({ startDate: '2025-04-07', endDate: '2025-04-13', locationId: 12 });
  });

  it('sends all IDs from a valid user filter', async () => {
    let body: unknown;
    server.use(
      http.post('*/api/training/calendar/events', async ({ request }) => {
        body = await request.json();
        return HttpResponse.json([]);
      }),
    );

    await calendarTrainingEventsContribution.load({ ...context, filters: { userIds: [userId, otherUserId] } });

    expect(body).toEqual({
      startDate: '2025-04-07',
      endDate: '2025-04-13',
      locationId: 12,
      userIds: [userId, otherUserId],
    });
  });

  it('returns no Training events or request for an entirely invalid filter while other contributions load', async () => {
    let requests = 0;
    server.use(
      http.post('*/api/training/calendar/events', () => {
        requests++;
        return HttpResponse.json([]);
      }),
    );
    const otherEvent = {
      id: 'other.1',
      type: 'calendar.general',
      sourceModule: 'calendar',
      title: 'Other event',
      start: '2025-04-08',
    };
    const registry = new CalendarRegistry();
    registry.registerModuleContribution(calendarTrainingEventsContribution);
    registry.registerModuleContribution({
      moduleId: 'calendar',
      contributionId: 'other.events',
      async load() {
        return { moduleId: 'calendar', contributionId: 'other.events', events: [otherEvent] };
      },
    });

    const result = await calendarDataService.loadData(
      {
        featureFlags: {
          Calendar: { source: 'Calendar', enabled: true, calendarMatrixTest: false },
          Training: { source: 'Training', enabled: true },
        },
        permissions: [Permissions.UserTrainingsView],
      },
      { ...context, filters: { userIds: ['', 'not-a-guid', '00000000-0000-0000-0000-000000000000', 42] } },
      registry,
    );

    expect(result.contributions['training.events']).toMatchObject({ events: [] });
    expect(result.contributions['other.events']?.events).toEqual([otherEvent]);
    expect(requests).toBe(0);
    calendarDataService.endSession();
  });

  it('returns empty events for an empty response', async () => {
    server.use(http.post('*/api/training/calendar/events', () => HttpResponse.json([])));
    await expect(calendarTrainingEventsContribution.load(context)).resolves.toMatchObject({ events: [] });
  });

  it('makes no request when Training is disabled or the permission is absent', async () => {
    let requests = 0;
    server.use(
      http.post('*/api/training/calendar/events', () => {
        requests++;
        return HttpResponse.json([]);
      }),
    );
    const registry = new CalendarRegistry();
    registry.registerModuleContribution(calendarTrainingEventsContribution);
    const enabled = {
      Calendar: { source: 'Calendar', enabled: true, calendarMatrixTest: false },
      Training: { source: 'Training', enabled: true },
    };

    const unauthorized = await calendarDataService.loadData(
      { featureFlags: enabled, permissions: [] },
      { ...context, activeViewId: 'calendar.matrix-assignment' },
      registry,
    );
    const disabled = await calendarDataService.loadData(
      {
        featureFlags: { ...enabled, Training: { ...enabled.Training, enabled: false } },
        permissions: [Permissions.UserTrainingsView],
      },
      { ...context, activeViewId: 'calendar.matrix-assignment' },
      registry,
    );

    expect(unauthorized.contributions).toEqual({});
    expect(disabled.contributions).toEqual({});
    expect(requests).toBe(0);
    calendarDataService.endSession();
  });

  it('loads warning-only training for filtered sidebar users regardless of their home location', async () => {
    const requests: Record<string, unknown>[] = [];
    server.use(
      http.get('*/api/users', ({ request }) => {
        expect(new URL(request.url).searchParams.get('LocationId')).toBe('12');
        return HttpResponse.json([
          { id: userId, homeLocationId: 99 },
          { id: otherUserId, homeLocationId: 12 },
        ]);
      }),
      http.post('*/api/training/calendar/events', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        requests.push(body);
        // The backend scopes LocationId to the user's home location.
        return HttpResponse.json(
          body.locationId
            ? []
            : [
                {
                  id: 'training.user-training.42',
                  userTrainingId: 42,
                  userId,
                  trainingId: 7,
                  type: 'training.user-training',
                  sourceModule: 'training',
                  title: 'Training: FIRST-AID',
                  start: '2025-04-08T00:00:00Z',
                  end: '2025-04-11T00:00:00Z',
                  allDay: true,
                  isReadOnly: true,
                  locationId: 99,
                },
              ],
        );
      }),
    );

    const result = await calendarTrainingEventsContribution.load({
      ...context,
      activeViewId: 'calendar.matrix-assignment',
      filters: { userIds: [userId] },
    });

    expect(requests).toHaveLength(2);
    expect(requests).toContainEqual({
      startDate: '2025-04-07',
      endDate: '2025-04-13',
      locationId: 12,
      userIds: [userId],
    });
    expect(requests).toContainEqual({
      startDate: '2025-04-07',
      endDate: '2025-04-13',
      userIds: [userId],
    });
    expect(result.events).toEqual([]);
    expect((result.data as TrainingCalendarEventsData).warningEvents).toEqual([
      expect.objectContaining({ userId, locationId: 99, start: '2025-04-08', end: '2025-04-11' }),
    ]);
  });

  it.each([
    { users: [], filters: {} },
    { users: [{ id: otherUserId, homeLocationId: 12 }], filters: { userIds: [userId] } },
  ])(
    'never sends an unrestricted warning request for an empty sidebar intersection: %o',
    async ({ users, filters }) => {
      const requests: Record<string, unknown>[] = [];
      server.use(
        http.get('*/api/users', () => HttpResponse.json(users)),
        http.post('*/api/training/calendar/events', async ({ request }) => {
          requests.push((await request.json()) as Record<string, unknown>);
          return HttpResponse.json([]);
        }),
      );
      const result = await calendarTrainingEventsContribution.load({
        ...context,
        activeViewId: 'calendar.matrix-assignment',
        filters,
      });
      expect(requests).toHaveLength(1);
      expect(requests[0]).toHaveProperty('locationId', 12);
      expect(result.data).toEqual({ warningEvents: [] });
    },
  );

  it('reloads warning users and dates after location and period changes, and omits warnings in Scheduling View', async () => {
    const warningRequests: Record<string, unknown>[] = [];
    let userRequests = 0;
    server.use(
      http.get('*/api/users', ({ request }) => {
        userRequests++;
        const id = new URL(request.url).searchParams.get('LocationId') === '12' ? userId : otherUserId;
        return HttpResponse.json([{ id, homeLocationId: 99 }]);
      }),
      http.post('*/api/training/calendar/events', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        if (!('locationId' in body)) warningRequests.push(body);
        return HttpResponse.json([]);
      }),
    );
    const assignment = { ...context, activeViewId: 'calendar.matrix-assignment', filters: {} };
    await calendarTrainingEventsContribution.load(assignment);
    await calendarTrainingEventsContribution.load({
      ...assignment,
      locationId: 13,
      startDate: '2025-04-14',
      endDate: '2025-04-15',
    });
    const scheduling = await calendarTrainingEventsContribution.load({
      ...assignment,
      activeViewId: 'calendar.matrix-shift',
    });
    expect(warningRequests).toEqual([
      { startDate: '2025-04-07', endDate: '2025-04-13', userIds: [userId] },
      { startDate: '2025-04-14', endDate: '2025-04-14', userIds: [otherUserId] },
    ]);
    expect(userRequests).toBe(2);
    expect(scheduling.data).toEqual({ warningEvents: [] });
  });

  it.each([undefined, 'calendar.matrix-assignment'])('honors request cancellation in view %s', async (activeViewId) => {
    let requestStarted!: () => void;
    const started = new Promise<void>((resolve) => {
      requestStarted = resolve;
    });
    let finishRequest!: () => void;
    const finished = new Promise<void>((resolve) => {
      finishRequest = resolve;
    });
    server.use(
      http.get('*/api/users', () => HttpResponse.json([{ id: userId, homeLocationId: 99 }])),
      http.post('*/api/training/calendar/events', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        if (activeViewId && body.locationId) return HttpResponse.json([]);
        requestStarted();
        await finished;
        return HttpResponse.json([]);
      }),
    );

    const controller = new AbortController();
    const pending = calendarTrainingEventsContribution.load(
      { ...context, activeViewId },
      { signal: controller.signal },
    );
    await started;
    controller.abort();
    finishRequest();

    await expect(pending).rejects.toBeTruthy();
  });

  it.each([undefined, 'calendar.matrix-assignment'])('propagates request failures in view %s', async (activeViewId) => {
    server.use(
      http.get('*/api/users', () => HttpResponse.json([{ id: userId, homeLocationId: 99 }])),
      http.post('*/api/training/calendar/events', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        return activeViewId && body.locationId
          ? HttpResponse.json([])
          : HttpResponse.json({ detail: 'Unavailable' }, { status: 500 });
      }),
    );
    await expect(calendarTrainingEventsContribution.load({ ...context, activeViewId })).rejects.toThrow('Unavailable');
  });
});
