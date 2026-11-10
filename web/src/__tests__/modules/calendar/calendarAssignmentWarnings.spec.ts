import { beforeEach, describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import CalendarMatrixSidePanelItem from '@/modules/calendar/components/matrix/CalendarMatrixSidePanelItem.vue';
import { CALENDAR_ALL_DAY_LABEL } from '@/utils/date';
import { Permissions } from '@/api-access/generated/models';
import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { calendarSchedulingEventsContribution } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import { calendarSchedulingLoanOutContribution } from '@/modules/scheduling/contributions/calendarSchedulingLoanOutContribution';
import { useLocationsStore } from '@/stores/LocationsStore';
import type {
  CalendarDataResponse,
  CalendarQueryContext,
  CalendarRuntimeContext,
} from '@/modules/calendar/calendarTypes';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import type { CalendarSchedulingUserResource } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import type { CalendarSchedulingAssignmentResource } from '@/modules/scheduling/contributions/calendarSchedulingAssignmentResourcesContribution';
import {
  calendarTrainingEventsContribution,
  type TrainingCalendarEvent,
} from '@/modules/training/contributions/calendarTrainingEventsContribution';
import { calendarAssignmentViewContribution } from '@/modules/scheduling/calendarAssignmentViewContribution';
import {
  buildCalendarAssignmentViewModel,
  buildCalendarSchedulingViewModel,
} from '@/modules/scheduling/calendarSchedulingMappers';

const context: CalendarQueryContext = {
  startDate: '2025-03-09',
  endDate: '2025-03-10',
  locationId: 1,
  filters: { timeZoneId: 'America/Vancouver' },
};
const runtime: CalendarRuntimeContext = {
  featureFlags: {},
  permissions: [Permissions.LeaveView, Permissions.AssignmentsView],
};

function leave(
  id: string,
  start: string,
  end?: string,
  extra: Partial<CalendarSchedulingEvent> = {},
): CalendarSchedulingEvent {
  return {
    id,
    start,
    end,
    title: id,
    type: 'scheduling.leave',
    sourceModule: 'leave',
    metadata: { userIds: ['user-1'] },
    ...extra,
  };
}

function training(
  id: string,
  start: string,
  end: string,
  extra: Partial<TrainingCalendarEvent> = {},
): TrainingCalendarEvent {
  return {
    id,
    start,
    end,
    title: id,
    type: 'training.user-training',
    sourceModule: 'training',
    userTrainingId: 1,
    userId: 'user-1',
    trainingId: 1,
    resourceIds: ['user-1'],
    allDay: true,
    locationId: 1,
    ...extra,
  };
}

const trainingRuntime: CalendarRuntimeContext = {
  featureFlags: { Calendar: { enabled: true }, Training: { enabled: true } },
  permissions: [...runtime.permissions!, Permissions.UserTrainingsView],
};
const loanOutRuntime: CalendarRuntimeContext = {
  featureFlags: {
    ...trainingRuntime.featureFlags,
    Scheduling: { enabled: true },
    Leave: { enabled: true },
    LoanOut: { enabled: true },
  },
  permissions: [...trainingRuntime.permissions!, Permissions.LoanOutView],
};

function loanOut(id: string, start: string, end?: string, extra: Partial<CalendarSchedulingEvent> = {}) {
  return leave(id, start, end, {
    type: 'scheduling.LoanOut',
    sourceModule: 'loanout',
    locationId: 1,
    metadata: { userIds: ['user-1'], loanOutLocationId: 2 },
    ...extra,
  });
}

function response(
  events: CalendarSchedulingEvent[],
  trainingEvents: TrainingCalendarEvent[] = [],
  query = context,
): CalendarDataResponse {
  const resources: CalendarSchedulingUserResource[] = ['user-1', 'user-2'].map((id) => ({
    id,
    title: id,
    label: id,
    type: 'user',
    sourceModule: 'scheduling',
  }));
  return {
    contributions: {
      'scheduling.events': {
        moduleId: 'scheduling',
        contributionId: 'scheduling.events',
        events: events.filter((event) => event.locationId == null || event.locationId === query.locationId),
        resources,
      },
      'training.events': {
        moduleId: 'training',
        contributionId: 'training.events',
        events: [],
        data: { warningEvents: trainingEvents },
      },
    },
  };
}

function items(events: CalendarSchedulingEvent[], query = context, permissions = runtime) {
  return buildCalendarAssignmentViewModel(response(events, [], query), query, 'day', permissions).sidePanel!.items;
}

describe('assignment sidebar warnings', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    useLocationsStore().entities = [
      { id: 1, name: 'Vancouver', timezone: 'America/Vancouver' },
      { id: 2, name: 'Edmonton', timezone: 'America/Edmonton' },
      { id: 3, name: 'Surrey', timezone: 'America/Vancouver' },
    ];
  });

  it('shows a home employee loan-out with origin and destination without changing schedule warnings or dragging', () => {
    const event = loanOut('out', '2025-03-09T17:00:00Z', '2025-03-09T20:00:00Z');
    const data = response([event]);
    const item = buildCalendarAssignmentViewModel(data, context, 'day', loanOutRuntime).sidePanel!.items[0]!;
    expect(item.warningGroups).toEqual([
      {
        id: 'loanout',
        heading: 'Loan Out',
        warnings: [
          { id: 'out', title: 'From: Vancouver; To: Edmonton', start: event.start, end: event.end, allDay: false },
        ],
      },
    ]);
    expect(item.draggable).toBe(true);
    expect(item.payload).toMatchObject({ userId: 'user-1', sourceModule: 'scheduling' });
    expect(
      buildCalendarSchedulingViewModel(data, context, 'day').sidePanel!.items.every(
        (sidebarItem) => sidebarItem.warningGroups === undefined,
      ),
    ).toBe(true);
    expect(items([event], context, trainingRuntime)[0]!.warningGroups).toBeUndefined();
    expect(
      items([event], context, { ...loanOutRuntime, featureFlags: { LoanOut: { enabled: false } } })[0]!.warningGroups,
    ).toBeUndefined();
  });

  it('orders overlapping loan-out occurrences across DST boundaries and removes cancellations and duplicate metadata', () => {
    const events = [
      loanOut('later', '2025-03-09T20:00:00Z', '2025-03-10T10:00:00Z'),
      loanOut('earlier', '2025-03-08T20:00:00Z', '2025-03-09T09:00:00Z'),
      loanOut('middle', '2025-03-09T17:00:00Z', '2025-03-09T19:00:00Z'),
      loanOut('same-time-other-record', '2025-03-09T17:00:00Z', '2025-03-09T19:00:00Z'),
      loanOut('cancelled', '2025-03-09T18:00:00Z', '2025-03-09T19:00:00Z', { statusTypeCode: 'cancelled' }),
      loanOut('cancelled-at', '2025-03-09T19:00:00Z', '2025-03-09T20:00:00Z', {
        cancelledAt: '2025-03-08T00:00:00Z',
      }),
      loanOut('ends-at-start', '2025-03-08T20:00:00Z', '2025-03-09T08:00:00Z'),
      loanOut('starts-at-end', '2025-03-10T07:00:00Z', '2025-03-10T10:00:00Z'),
    ];
    const data = response([...events, events[2]!]);
    data.contributions['scheduling.loanout'] = {
      moduleId: 'loanout',
      contributionId: 'scheduling.loanout',
      events: [],
      resources: [
        {
          id: 'user-1',
          type: 'user',
          sourceModule: 'loanout',
          label: 'One',
          title: 'One',
          homeLocationId: 1,
          loanOutMetaData: events.map((event) => ({
            loanOutLocationId: 2,
            startAtUtc: event.start,
            endAtUtc: event.end,
          })),
        } as CalendarSchedulingUserResource,
      ],
    };
    const warnings = buildCalendarAssignmentViewModel(data, context, 'day', loanOutRuntime).sidePanel!.items[0]!
      .warningGroups![0]!.warnings;
    expect(warnings.map((warning) => warning.id)).toEqual(['earlier', 'middle', 'same-time-other-record', 'later']);
    expect(warnings[0]).toMatchObject({ start: '2025-03-08T20:00:00Z', end: '2025-03-09T09:00:00Z' });
    expect(warnings[3]).toMatchObject({ end: '2025-03-10T10:00:00Z' });
  });

  it('loads borrowed home-location training and warns once with original leave times while preserving projections', async () => {
    const borrowedId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const borrowedTraining = training('borrowed-training', '2025-03-09', '2025-03-10', {
      userId: borrowedId,
      resourceIds: [borrowedId],
      locationId: 2,
    });
    const trainingRecords = [
      training('home-warning', '2025-03-09', '2025-03-10'),
      borrowedTraining,
      training('unrelated-training', '2025-03-09', '2025-03-10', {
        userId: 'other-user',
        resourceIds: ['other-user'],
        locationId: 99,
      }),
    ];
    const trainingRequests: Array<{ locationId?: number; userIds?: string[] }> = [];
    let userRequests = 0;
    let leaveRequests = 0;
    server.use(
      http.get('*/api/users', () => HttpResponse.json([{ id: 'user-1', homeLocationId: 1 }])),
      http.post('*/api/training/calendar/events', async ({ request }) => {
        const body = (await request.json()) as { locationId?: number; userIds?: string[] };
        trainingRequests.push(body);
        // Match the backend: explicit IDs take precedence over the selected location.
        const records = trainingRecords.filter((record) =>
          body.userIds?.length ? body.userIds.includes(record.userId) : record.locationId === body.locationId,
        );
        return HttpResponse.json(records.flatMap((record) => [record, record]));
      }),
      http.post('*/api/loan-out/scheduling/users', async ({ request }) => {
        userRequests++;
        expect(await request.json()).toMatchObject({
          locationId: 1,
          endDate: '2025-03-09',
          timeZoneId: 'America/Vancouver',
        });
        return HttpResponse.json([
          {
            id: borrowedId,
            firstName: 'Borrowed',
            lastName: 'User',
            idirName: 'borrowed',
            homeLocationId: 2,
            loanOutMetaData: [
              { loanOutLocationId: 1, startAtUtc: '2025-03-09T17:00:00Z', endAtUtc: '2025-03-09T21:00:00Z' },
              { loanOutLocationId: 1, startAtUtc: '2025-03-09T17:00:00.000Z', endAtUtc: '2025-03-09T21:00:00.000Z' },
              { loanOutLocationId: 1, startAtUtc: '2025-03-09T22:00:00Z', endAtUtc: '2025-03-09T22:30:00Z' },
            ],
          },
        ]);
      }),
      http.post('*/api/leave/calendar/events', async ({ request }) => {
        leaveRequests++;
        expect(await request.json()).toMatchObject({ userIds: [borrowedId] });
        const event = {
          id: 'scheduling.leave-entry.7',
          type: 'scheduling.leave',
          sourceModule: 'leave',
          title: 'Vacation',
          start: '2025-03-09T15:00:00Z',
          end: '2025-03-09T23:00:00Z',
          locationId: 2,
          userIds: [borrowedId],
          leaveEntryId: 7,
          leaveTypeName: 'Vacation',
        };
        return HttpResponse.json([event, event]);
      }),
    );
    const borrowed = await calendarSchedulingLoanOutContribution.load(context, { runtimeContext: loanOutRuntime });
    expect(borrowed.resources![0]).toMatchObject({ homeLocationId: 2 });
    expect(borrowed.events.map((event) => [event.start, event.end])).toEqual([
      ['2025-03-09T17:00:00.000Z', '2025-03-09T21:00:00.000Z'],
      ['2025-03-09T22:00:00.000Z', '2025-03-09T22:30:00.000Z'],
      ['2025-03-09T17:00:00.000Z', '2025-03-09T21:00:00.000Z'],
      ['2025-03-09T22:00:00.000Z', '2025-03-09T22:30:00.000Z'],
    ]);
    const data = response([]);
    data.contributions['scheduling.loanout'] = borrowed;
    data.contributions['training.events'] = await calendarTrainingEventsContribution.load(
      { ...context, activeViewId: 'calendar.matrix-assignment' },
      { runtimeContext: loanOutRuntime },
    );
    expect(trainingRequests).toHaveLength(3);
    expect(trainingRequests).toContainEqual({
      startDate: '2025-03-09',
      endDate: '2025-03-09',
      locationId: 1,
      timeZoneId: 'America/Vancouver',
      userIds: [borrowedId],
    });
    expect(data.contributions['training.events'].events.map((event) => event.id)).toEqual([
      'home-warning',
      'borrowed-training',
    ]);
    const sidebar = () => buildCalendarAssignmentViewModel(data, context, 'day', loanOutRuntime).sidePanel!.items;
    const item = sidebar().find((user) => user.id === borrowedId)!;
    expect(item.warningGroups?.map((group) => group.heading)).toEqual(['Leave', 'Training', 'Loan Out']);
    expect(item.warningGroups?.map((group) => group.warnings.length)).toEqual([1, 1, 2]);
    expect(item.warningGroups![0]!.warnings[0]).toMatchObject({
      id: 'scheduling.leave-entry.7',
      title: 'Vacation',
      start: '2025-03-09T15:00:00Z',
      end: '2025-03-09T23:00:00Z',
    });
    expect(item.warningGroups![1]!.warnings[0]).toMatchObject({ id: 'borrowed-training' });
    expect(item.warningGroups![2]!.warnings[0]).toMatchObject({ title: 'From: Edmonton; To: Vancouver' });
    expect(
      sidebar()
        .find((user) => user.id === 'user-1')!
        .warningGroups![0]!.warnings.map((warning) => warning.id),
    ).toEqual(['home-warning']);
    expect(sidebar().find((user) => user.id === 'user-2')!.warningGroups).toBeUndefined();
    expect(item.payload).toMatchObject({
      userId: borrowedId,
      sourceModule: 'loanout',
      loanOutMetaData: expect.any(Array),
    });
    // Each contribution retains its existing borrower lookup; warning mapping adds no requests.
    expect(userRequests).toBe(2);
    expect(leaveRequests).toBe(1);
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarMatrixSidePanelItem, {
      props: { item, timeZone: 'America/Vancouver' },
      global: {
        plugins: app.mountPlugins,
        stubs: { VTooltip: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' } },
      },
    });
    const leaveRows = wrapper.findAll('li').filter((row) => row.text().includes('Vacation'));
    expect(leaveRows).toHaveLength(1);
    expect(leaveRows[0]!.text()).toContain('March 9, 2025, 8:00 AM - 4:00 PM');
    wrapper.unmount();
  });

  it.each([true, false])(
    'restores original leave across calendar/DST boundaries without expanding projected users (allDay=%s)',
    async (allDay) => {
      const borrowedId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
      const original = {
        id: 'leave-occurrence',
        type: 'scheduling.leave',
        sourceModule: 'leave',
        title: 'Vacation',
        start: '2025-03-08T07:00:00Z',
        end: '2025-03-11T06:00:00Z',
        allDay,
        timeZoneId: 'America/Edmonton',
        locationId: 2,
        leaveTypeName: 'Vacation',
        userIds: [borrowedId, 'user-1'],
      };
      server.use(
        http.post('*/api/loan-out/scheduling/users', () =>
          HttpResponse.json([
            {
              id: borrowedId,
              firstName: 'Borrowed',
              lastName: 'User',
              idirName: 'borrowed',
              homeLocationId: 2,
              loanOutMetaData: [{ loanOutLocationId: 1, startAtUtc: original.start, endAtUtc: original.end }],
            },
          ]),
        ),
        http.post('*/api/leave/calendar/events', () => HttpResponse.json([original, original])),
      );
      const borrowed = await calendarSchedulingLoanOutContribution.load(context, { runtimeContext: loanOutRuntime });
      expect(borrowed.events).toHaveLength(2);
      expect(borrowed.events[0]).toMatchObject({
        start: '2025-03-09T08:00:00.000Z',
        end: '2025-03-10T07:00:00.000Z',
        resourceIds: [borrowedId],
        metadata: { originalLeaveOccurrence: { id: original.id, start: original.start, end: original.end } },
      });
      const data = response([]);
      data.contributions['scheduling.loanout'] = borrowed;
      const sidebar = buildCalendarAssignmentViewModel(data, context, 'day', loanOutRuntime).sidePanel!.items;
      const item = sidebar.find((user) => user.id === borrowedId)!;
      const warnings = item.warningGroups!.find((group) => group.id === 'leave')!.warnings;
      expect(warnings).toEqual([
        expect.objectContaining({
          id: original.id,
          allDay,
          start: allDay ? '2025-03-08' : original.start,
          end: allDay ? '2025-03-11' : original.end,
        }),
      ]);
      expect(sidebar.find((user) => user.id === 'user-1')!.warningGroups).toBeUndefined();
      const app = await createTestApp({ loadConfig: false });
      const wrapper = mount(CalendarMatrixSidePanelItem, {
        props: { item, timeZone: 'America/Vancouver' },
        global: {
          plugins: app.mountPlugins,
          stubs: { VTooltip: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' } },
        },
      });
      const leaveRows = wrapper.findAll('li').filter((row) => row.text().includes('Vacation'));
      expect(leaveRows).toHaveLength(1);
      expect(leaveRows[0]!.text()).toContain(
        allDay
          ? `March 8, 2025 - March 10, 2025, ${CALENDAR_ALL_DAY_LABEL}`
          : 'March 7, 2025, 11:00 PM - March 10, 2025, 11:00 PM',
      );
      wrapper.unmount();
    },
  );

  it('handles missing optional loan-out metadata and dates without dropping existing warnings', () => {
    const data = response([
      loanOut('unknown-location', '2025-03-09T17:00:00Z', undefined, {
        locationId: undefined,
        metadata: { userIds: ['user-1'] },
      }),
      leave('leave', '2025-03-09T18:00:00Z'),
    ]);
    data.contributions['scheduling.loanout'] = {
      moduleId: 'loanout',
      contributionId: 'scheduling.loanout',
      events: [],
      resources: [
        {
          id: 'borrowed',
          type: 'user',
          sourceModule: 'loanout',
          label: 'Borrowed',
          title: 'Borrowed',
          loanOutMetaData: [{ loanOutLocationId: 1 }, { startAtUtc: 'invalid' }],
        } as CalendarSchedulingUserResource,
      ],
    };
    const sidebar = buildCalendarAssignmentViewModel(data, context, 'day', loanOutRuntime).sidePanel!.items;
    expect(sidebar[0]!.warningGroups?.map((group) => group.heading)).toEqual(['Leave', 'Loan Out']);
    expect(sidebar[0]!.warningGroups![1]!.warnings[0]).toMatchObject({
      title: 'From: Unknown location; To: Location -',
      start: '2025-03-09T17:00:00Z',
      end: undefined,
    });
    expect(sidebar.find((user) => user.id === 'borrowed')!.warningGroups).toBeUndefined();
  });

  it.each([
    {
      name: 'single-day Edmonton leave',
      start: '2025-01-13T07:00:00Z',
      end: '2025-01-14T07:00:00Z',
      timeZoneId: 'America/Edmonton',
      first: '2025-01-13',
      exclusiveEnd: '2025-01-14',
      dates: 'January 13, 2025',
    },
    {
      name: 'multi-day Edmonton leave',
      start: '2025-01-13T07:00:00Z',
      end: '2025-01-15T07:00:00Z',
      timeZoneId: 'America/Edmonton',
      first: '2025-01-13',
      exclusiveEnd: '2025-01-15',
      dates: 'January 13, 2025 - January 14, 2025',
    },
    {
      name: 'Vancouver leave',
      start: '2025-01-13T08:00:00Z',
      end: '2025-01-15T08:00:00Z',
      timeZoneId: 'America/Vancouver',
      first: '2025-01-13',
      exclusiveEnd: '2025-01-15',
      dates: 'January 13, 2025 - January 14, 2025',
    },
    {
      name: 'leave without a source zone',
      start: '2025-01-13T08:00:00Z',
      end: '2025-01-15T08:00:00Z',
      timeZoneId: undefined,
      first: '2025-01-13',
      exclusiveEnd: '2025-01-15',
      dates: 'January 13, 2025 - January 14, 2025',
    },
    {
      name: 'Edmonton leave across the spring DST boundary',
      start: '2025-03-08T07:00:00Z',
      end: '2025-03-11T06:00:00Z',
      timeZoneId: 'America/Edmonton',
      first: '2025-03-08',
      exclusiveEnd: '2025-03-11',
      dates: 'March 8, 2025 - March 10, 2025',
    },
  ])(
    'preserves source dates and the exclusive end for $name',
    async ({ start, end, timeZoneId, first, exclusiveEnd, dates }) => {
      const query = { ...context, startDate: first, endDate: exclusiveEnd };
      const item = items([leave('vacation', start, end, { allDay: true, timeZoneId })], query)[0]!;
      expect(item.warningGroups?.[0]?.warnings).toEqual([
        expect.objectContaining({ start: first, end: exclusiveEnd, allDay: true }),
      ]);
      const app = await createTestApp({ loadConfig: false });
      const wrapper = mount(CalendarMatrixSidePanelItem, {
        props: { item, timeZone: 'America/Vancouver' },
        global: {
          plugins: app.mountPlugins,
          stubs: {
            VTooltip: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' },
          },
        },
      });
      expect(wrapper.text()).toContain(`${dates}, ${CALENDAR_ALL_DAY_LABEL}`);
      wrapper.unmount();
    },
  );

  it('preserves date-only leave and training even when a source timezone is provided', () => {
    const query = { ...context, startDate: '2025-01-13', endDate: '2025-01-14' };
    const data = response(
      [leave('leave', '2025-01-13', '2025-01-15', { allDay: true, timeZoneId: 'America/Edmonton' })],
      [training('training', '2025-01-13', '2025-01-15', { timeZoneId: 'America/Edmonton' })],
      query,
    );
    const groups = buildCalendarAssignmentViewModel(data, query, 'day', trainingRuntime).sidePanel!.items[0]!
      .warningGroups!;
    expect(groups.map((group) => group.warnings[0])).toEqual([
      expect.objectContaining({ start: '2025-01-13', end: '2025-01-15', allDay: true }),
      expect.objectContaining({ start: '2025-01-13', end: '2025-01-15', allDay: true }),
    ]);
  });

  it('keeps timed leave and training in the matrix timezone', async () => {
    const query = { ...context, startDate: '2025-01-13', endDate: '2025-01-14' };
    const start = '2025-01-13T17:00:00Z';
    const end = '2025-01-13T18:00:00Z';
    const data = response(
      [leave('leave', start, end, { timeZoneId: 'America/Edmonton' })],
      [training('training', start, end, { allDay: false, timeZoneId: 'America/Edmonton' })],
      query,
    );
    const item = buildCalendarAssignmentViewModel(data, query, 'day', trainingRuntime).sidePanel!.items[0]!;
    expect(item.warningGroups!.flatMap((group) => group.warnings)).toEqual([
      expect.objectContaining({ start, end }),
      expect.objectContaining({ start, end, allDay: false }),
    ]);
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarMatrixSidePanelItem, {
      props: { item, timeZone: 'America/Vancouver' },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          VTooltip: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' },
        },
      },
    });
    expect(wrapper.findAll('li').map((warning) => warning.text())).toEqual([
      expect.stringContaining('January 13, 2025, 9:00 AM - 10:00 AM'),
      expect.stringContaining('January 13, 2025, 9:00 AM - 10:00 AM'),
    ]);
    wrapper.unmount();
  });

  it('groups warning-only training across locations with leave and filters by user, period, and cancellation', () => {
    const data = response(
      [leave('leave', '2025-03-09T17:00:00Z')],
      [
        training('later', '2025-03-09', '2025-03-11'),
        training('crosses-start', '2025-03-08', '2025-03-10'),
        training('other-location', '2025-03-09', '2025-03-10', { locationId: 2 }),
        training('no-location', '2025-03-09', '2025-03-10', { locationId: undefined }),
        training('other-user', '2025-03-09', '2025-03-10', { userId: 'other', resourceIds: ['other'] }),
        training('ends-at-start', '2025-03-08', '2025-03-09'),
        training('starts-at-end', '2025-03-10', '2025-03-11'),
        training('cancelled', '2025-03-09', '2025-03-10', { statusTypeCode: 'cancelled' }),
        training('cancelled-at', '2025-03-09', '2025-03-10', { cancelledAt: '2025-03-08T00:00:00Z' }),
      ],
    );
    const groups = (query = context, access = trainingRuntime) =>
      buildCalendarAssignmentViewModel(data, query, 'day', access).sidePanel!.items[0]!.warningGroups;
    expect(groups()).toEqual([
      { id: 'leave', heading: 'Leave', warnings: [expect.objectContaining({ id: 'leave' })] },
      {
        id: 'training',
        heading: 'Training',
        warnings: [
          expect.objectContaining({ id: 'crosses-start', start: '2025-03-08', end: '2025-03-10', allDay: true }),
          expect.objectContaining({ id: 'later' }),
          expect.objectContaining({ id: 'no-location' }),
          expect.objectContaining({ id: 'other-location' }),
        ],
      },
    ]);
    expect(groups({ ...context, locationId: 2 })![1]).toEqual(groups()![1]);
    expect(groups({ ...context, startDate: '2025-03-12', endDate: '2025-03-13' })).toBeUndefined();
    expect(groups(context, { ...trainingRuntime, permissions: [Permissions.UserTrainingsView] })).toEqual([
      expect.objectContaining({ heading: 'Training' }),
    ]);
    expect(
      buildCalendarSchedulingViewModel(data, context, 'day').sidePanel!.items.every(
        (item) => item.warningGroups === undefined,
      ),
    ).toBe(true);
    expect(
      buildCalendarAssignmentViewModel(data, context, 'day', trainingRuntime).sidePanel!.items[1]!.warningGroups,
    ).toBeUndefined();
  });

  it('keeps normal training calendar events separate from warning-only results', () => {
    const data = response([]);
    data.contributions['training.events']!.events = [training('normal-event', '2025-03-09', '2025-03-10')];
    expect(
      buildCalendarAssignmentViewModel(data, context, 'day', trainingRuntime).sidePanel!.items[0]!.warningGroups,
    ).toBeUndefined();
  });

  it.each([
    { ...trainingRuntime, permissions: [Permissions.LeaveView] },
    { ...trainingRuntime, featureFlags: { Calendar: { enabled: true }, Training: { enabled: false } } },
    { ...trainingRuntime, featureFlags: { Calendar: { enabled: false }, Training: { enabled: true } } },
  ])('omits training warnings when its contribution is unavailable: %o', (access) => {
    const data = response([], [training('training', '2025-03-09', '2025-03-10')]);
    expect(
      buildCalendarAssignmentViewModel(data, context, 'day', access).sidePanel!.items[0]!.warningGroups,
    ).toBeUndefined();
  });

  it('retains the selected location in scheduling requests and uses the scoped leave response', async () => {
    setActivePinia(createPinia());
    const requests: Record<string, unknown>[] = [];
    server.use(
      http.get('*/api/lookup/PositionTypes', () => HttpResponse.json([{ code: 'Chief Sheriff', displayOrder: 1 }])),
      http.get('*/api/users', () => HttpResponse.json([])),
      http.post('*/api/scheduling/calendar/events', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        requests.push(body);
        const events = [
          leave('local', '2025-03-09T17:00:00Z', undefined, { locationId: 1 }),
          leave('remote', '2025-03-09T18:00:00Z', undefined, { locationId: 2 }),
          leave('global', '2025-03-09T19:00:00Z'),
          leave('remote-shift', '2025-03-09T20:00:00Z', undefined, { locationId: 2, type: 'scheduling.shift' }),
        ];
        return HttpResponse.json({
          events: events
            .filter((event) => event.locationId == null || event.locationId === body.locationId)
            .map((event) => ({ ...event, userIds: ['user-1'] })),
        });
      }),
    );
    const result = await calendarSchedulingEventsContribution.load({
      ...context,
      filters: { ...context.filters, userIds: ['user-1'] },
    });
    expect(requests).toHaveLength(1);
    expect(requests[0]).toHaveProperty('locationId', context.locationId);
    expect(requests[0]).toMatchObject({
      timeZoneId: 'America/Vancouver',
      startDate: context.startDate,
      userIds: ['user-1'],
    });
    expect(result.events.map((event) => event.id)).toEqual(['local', 'global']);
    expect(result.data).toEqual({ warningEvents: [] });
    expect(
      buildCalendarAssignmentViewModel(
        {
          contributions: {
            'scheduling.events': { ...result, resources: response([]).contributions['scheduling.events']!.resources },
          },
        },
        context,
        'day',
        runtime,
      ).sidePanel!.items[0]!.warningGroups?.[0]?.warnings.map((warning) => warning.id),
    ).toEqual(['local', 'global']);
  });

  it('groups location-scoped overlapping leave chronologically using the calendar timezone and DST boundaries', () => {
    const events = [
      leave('overlap', '2025-03-09T18:00:00Z', '2025-03-09T20:00:00Z'),
      leave('other-location', '2025-03-09T17:00:00Z', '2025-03-09T21:00:00Z', { locationId: 2 }),
      leave('crosses-start', '2025-03-08T20:00:00Z', '2025-03-09T09:00:00Z'),
      leave('ends-at-start', '2025-03-08T20:00:00Z', '2025-03-09T08:00:00Z'),
      leave('starts-at-end', '2025-03-10T07:00:00Z', '2025-03-10T08:00:00Z'),
      leave('after', '2025-03-11T08:00:00Z', '2025-03-11T09:00:00Z'),
      leave('cancelled', '2025-03-09T17:00:00Z', undefined, { statusTypeCode: 'cancelled' }),
      leave('cancelled-at', '2025-03-09T17:00:00Z', undefined, { cancelledAt: '2025-03-08T00:00:00Z' }),
      leave('another-user', '2025-03-09T17:00:00Z', undefined, { metadata: { userIds: ['other'] } }),
      leave('shift', '2025-03-09T17:00:00Z', undefined, { type: 'scheduling.shift' }),
    ];
    const [user, emptyUser] = items(events);
    expect(user!.warningGroups).toEqual([
      {
        id: 'leave',
        heading: 'Leave',
        warnings: [expect.objectContaining({ id: 'crosses-start' }), expect.objectContaining({ id: 'overlap' })],
      },
    ]);
    expect(emptyUser!.warningGroups).toBeUndefined();
    expect(
      items(events, { ...context, locationId: 2 })[0]!.warningGroups?.[0]?.warnings.map((warning) => warning.id),
    ).toEqual(['crosses-start', 'other-location', 'overlap']);
    expect(
      items(events, { ...context, startDate: '2025-03-12', endDate: '2025-03-13' })[0]!.warningGroups,
    ).toBeUndefined();
  });

  it('handles date-only and timestamp all-day leave, resource ownership, and empty or refreshed data', () => {
    const events = [
      leave('date-only', '2025-03-09', undefined, { allDay: true }),
      leave('multi-day', '2025-03-08T08:00:00Z', '2025-03-11T07:00:00Z', { allDay: true }),
      leave('resource', '2025-03-09T19:00:00Z', undefined, { metadata: {}, resourceIds: ['user-1'] }),
    ];
    expect(items(events)[0]!.warningGroups?.[0]?.warnings).toEqual([
      expect.objectContaining({ id: 'multi-day', start: '2025-03-08', end: '2025-03-11', allDay: true }),
      expect.objectContaining({ id: 'date-only', start: '2025-03-09', allDay: true }),
      expect.objectContaining({ id: 'resource' }),
    ]);
    expect(items([]).every((item) => item.warningGroups === undefined)).toBe(true);
    expect(buildCalendarAssignmentViewModel({ contributions: {} }, context, 'day', runtime).sidePanel?.items).toEqual(
      [],
    );
  });

  it('uses the displayed days for work-week and week periods', () => {
    const events = [
      leave('crosses-monday', '2025-03-09T07:00:00Z', '2025-03-10T09:00:00Z'),
      leave('friday', '2025-03-14T18:00:00Z', '2025-03-15T08:00:00Z'),
      leave('saturday', '2025-03-15T17:00:00Z', '2025-03-15T18:00:00Z'),
      leave('next-week', '2025-03-17T07:00:00Z', '2025-03-17T08:00:00Z'),
    ];
    const query = { ...context, startDate: '2025-03-12', endDate: '2025-03-17' };
    const warnings = (period: 'work-week' | 'week') =>
      buildCalendarAssignmentViewModel(
        response(events),
        query,
        period,
        runtime,
      ).sidePanel!.items[0]!.warningGroups![0]!.warnings.map((warning) => warning.id);
    expect(warnings('work-week')).toEqual(['crosses-monday', 'friday']);
    expect(warnings('week')).toEqual(['crosses-monday', 'friday', 'saturday']);
  });

  it('populates warnings through the assignment view only and respects leave access and feature flags', () => {
    const data = response([leave('leave', '2025-03-09T17:00:00Z')]);
    const assignmentResource: CalendarSchedulingAssignmentResource = {
      id: 'assignment-definition-1',
      type: 'assignment',
      sourceModule: 'scheduling',
      label: 'Room',
      title: 'Room',
      assignmentDefinitionId: 1,
    };
    data.contributions['scheduling.assignment-resources'] = {
      moduleId: 'scheduling',
      contributionId: 'scheduling.assignment-resources',
      events: [],
      resources: [assignmentResource],
    };
    const assignment = calendarAssignmentViewContribution.buildModel(data, context, runtime, 'day') as ReturnType<
      typeof buildCalendarAssignmentViewModel
    >;
    expect(assignment.sidePanel!.items[0]!.warningGroups).toHaveLength(1);
    const schedule = buildCalendarSchedulingViewModel(data, context, 'day');
    expect(schedule.sidePanel!.items).toHaveLength(1);
    expect(schedule.sidePanel!.items.every((item) => item.warningGroups === undefined)).toBe(true);
    expect(schedule.primaryColumn.resources.every((resource) => !('warningGroups' in resource))).toBe(true);
    expect(
      items(data.contributions['scheduling.events']!.events as CalendarSchedulingEvent[], context, {
        ...runtime,
        permissions: [Permissions.AssignmentsView],
      })[0]!.warningGroups,
    ).toBeUndefined();
    expect(
      items([leave('leave', '2025-03-09T17:00:00Z')], context, {
        ...runtime,
        featureFlags: { Leave: { enabled: false } },
      })[0]!.warningGroups,
    ).toBeUndefined();
  });

  describe('cross-location leave warnings', () => {
    const assignmentContext = { ...context, activeViewId: 'calendar.matrix-assignment' };
    const borrowedId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const borrowedUser = {
      id: borrowedId,
      firstName: 'Borrowed',
      lastName: 'User',
      idirName: 'borrowed',
      homeLocationId: 2,
      loanOutMetaData: [{ loanOutLocationId: 1, startAtUtc: '2025-03-09T17:00:00Z', endAtUtc: '2025-03-09T21:00:00Z' }],
    };
    const localLeave = leave('scheduling.leave-entry.1', '2025-03-09T18:00:00Z', '2025-03-09T19:00:00Z', {
      locationId: 1,
      metadata: { userIds: ['user-1'], leaveEntryId: '1', leaveTypeName: 'Vacation' },
    });
    const shift = leave('shift', '2025-03-09T16:00:00Z', '2025-03-10T00:00:00Z', {
      type: 'scheduling.shift',
      sourceModule: 'scheduling',
      locationId: 1,
      resourceIds: ['user-1'],
    });
    const assignment = leave('assignment', shift.start, shift.end, {
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      locationId: 1,
      metadata: { userIds: ['user-1'], assignedUserIds: ['user-1'], capacity: 1 },
    });
    let leaveRequests: Record<string, unknown>[];
    let schedulingRequests: Record<string, unknown>[];

    beforeEach(() => {
      leaveRequests = [];
      schedulingRequests = [];
      server.use(
        http.get('*/api/lookup/PositionTypes', () => HttpResponse.json([{ code: 'Chief Sheriff', displayOrder: 1 }])),
        http.get('*/api/users', () =>
          HttpResponse.json(
            ['user-1', 'user-2'].map((id) => ({
              id,
              firstName: id,
              lastName: 'Home',
              homeLocationId: 1,
              rank: 'Chief Sheriff',
            })),
          ),
        ),
        http.get('*/api/users/:userId/acting-positions', () => HttpResponse.json([])),
        http.post('*/api/loan-out/scheduling/users', () => HttpResponse.json([borrowedUser])),
        http.post('*/api/scheduling/calendar/events', async ({ request }) => {
          schedulingRequests.push((await request.json()) as Record<string, unknown>);
          return HttpResponse.json({
            events: [localLeave, shift, assignment].map((event) => ({
              ...event,
              userIds: event.metadata.userIds,
              assignedUserIds: event.metadata.assignedUserIds,
              capacity: event.metadata.capacity,
              leaveTypeName: event.metadata.leaveTypeName,
            })),
          });
        }),
        http.post('*/api/leave/calendar/events', async ({ request }) => {
          leaveRequests.push((await request.json()) as Record<string, unknown>);
          return HttpResponse.json([]);
        }),
      );
    });

    it('loads home employees across locations, reuses warning filters and deduplication, and preserves scheduling displays', async () => {
      const remote = leave('remote', '2025-03-09T15:00:00Z', '2025-03-09T17:00:00Z', { locationId: 2 });
      const records = [
        localLeave,
        remote,
        remote,
        leave('crosses-start', '2025-03-08T20:00:00Z', '2025-03-09T09:00:00Z', { locationId: 2 }),
        leave('cancelled', remote.start, remote.end, { locationId: 2, statusTypeCode: 'Cancelled' }),
        leave('cancelled-at', remote.start, remote.end, { locationId: 2, cancelledAt: remote.start }),
        leave('ends-at-start', '2025-03-08T20:00:00Z', '2025-03-09T08:00:00Z', { locationId: 2 }),
        leave('starts-at-end', '2025-03-10T07:00:00Z', '2025-03-10T08:00:00Z', { locationId: 2 }),
        leave('other-user', remote.start, remote.end, { metadata: { userIds: ['other'] } }),
      ];
      server.use(
        http.post('*/api/leave/calendar/events', async ({ request }) => {
          const body = (await request.json()) as { userIds: string[] };
          leaveRequests.push(body);
          return HttpResponse.json(
            records
              .filter((event) => event.metadata.userIds?.some((id) => body.userIds.includes(id)))
              .map((event) => ({ ...event, userIds: event.metadata.userIds, leaveTypeName: 'Vacation' })),
          );
        }),
      );
      const result = await calendarSchedulingEventsContribution.load(assignmentContext, {
        runtimeContext: loanOutRuntime,
      });
      expect(leaveRequests).toEqual([
        {
          startAtUtc: '2025-03-09T08:00:00.000Z',
          endAtUtc: '2025-03-10T07:00:00.000Z',
          userIds: ['user-1', 'user-2'],
        },
      ]);
      expect(schedulingRequests).toEqual([
        {
          startDate: context.startDate,
          // The scheduling API uses an inclusive end date; Calendar ranges end exclusively.
          endDate: '2025-03-09',
          timeZoneId: 'America/Vancouver',
          locationId: 1,
        },
      ]);
      expect(result.events.map((event) => event.id)).toEqual([localLeave.id, shift.id, assignment.id]);
      const data = { contributions: { 'scheduling.events': result } };
      const model = buildCalendarAssignmentViewModel(data, assignmentContext, 'day', loanOutRuntime);
      expect(model.sidePanel!.items.map((item) => item.id)).toEqual(['user-1', 'user-2']);
      expect(model.sidePanel!.items[0]!.warningGroups).toEqual([
        {
          id: 'leave',
          heading: 'Leave',
          warnings: [
            expect.objectContaining({ id: 'crosses-start', start: '2025-03-08T20:00:00Z' }),
            expect.objectContaining({ id: remote.id, start: remote.start, end: remote.end }),
            expect.objectContaining({ id: localLeave.id }),
          ],
        },
      ]);
      expect(model.sidePanel!.items[1]!.warningGroups).toBeUndefined();
      // Warning-only leave must not enter schedule cells, assignments, or normal event selectors.
      const withoutWarnings = { contributions: { 'scheduling.events': { ...result, data: undefined } } };
      expect(buildCalendarSchedulingViewModel(data, context, 'day')).toEqual(
        buildCalendarSchedulingViewModel(withoutWarnings, context, 'day'),
      );
      expect(model.primaryColumn).toEqual(
        buildCalendarAssignmentViewModel(withoutWarnings, context, 'day', loanOutRuntime).primaryColumn,
      );
      for (const access of [
        { ...loanOutRuntime, permissions: [Permissions.AssignmentsView] },
        { ...loanOutRuntime, featureFlags: { ...loanOutRuntime.featureFlags, Leave: { enabled: false } } },
      ]) {
        expect(
          buildCalendarAssignmentViewModel(data, context, 'day', access).sidePanel!.items[0]!.warningGroups,
        ).toBeUndefined();
      }
    });

    it('reuses borrowed leave results outside loan-out windows while leaving projections and user membership unchanged', async () => {
      const outside = {
        id: 'outside-loan',
        type: 'scheduling.leave',
        sourceModule: 'leave',
        title: 'Vacation',
        leaveTypeName: 'Vacation',
        locationId: 3,
        userIds: [borrowedId, 'user-1'],
        start: '2025-03-09T09:00:00Z',
        end: '2025-03-09T10:00:00Z',
      };
      const overlapping = {
        ...outside,
        id: 'overlaps-loan',
        start: '2025-03-09T16:00:00Z',
        end: '2025-03-09T23:00:00Z',
      };
      server.use(
        http.post('*/api/leave/calendar/events', async ({ request }) => {
          leaveRequests.push((await request.json()) as Record<string, unknown>);
          return HttpResponse.json([outside, overlapping, outside]);
        }),
      );
      const borrowed = await calendarSchedulingLoanOutContribution.load(assignmentContext, {
        runtimeContext: loanOutRuntime,
      });
      expect(leaveRequests).toEqual([
        {
          startAtUtc: '2025-03-09T08:00:00.000Z',
          endAtUtc: '2025-03-10T07:00:00.000Z',
          userIds: [borrowedId],
        },
      ]);
      expect(borrowed.events).toHaveLength(1);
      expect(borrowed.events[0]).toMatchObject({
        start: '2025-03-09T17:00:00.000Z',
        end: '2025-03-09T21:00:00.000Z',
        resourceIds: [borrowedId],
        isReadOnly: true,
      });
      const data = response([]);
      data.contributions['scheduling.loanout'] = borrowed;
      const sidebar = buildCalendarAssignmentViewModel(data, assignmentContext, 'day', loanOutRuntime).sidePanel!.items;
      expect(sidebar.find((item) => item.id === borrowedId)!.warningGroups![0]).toEqual({
        id: 'leave',
        heading: 'Leave',
        warnings: [
          expect.objectContaining({ id: outside.id, start: outside.start, end: outside.end }),
          expect.objectContaining({ id: overlapping.id, start: overlapping.start, end: overlapping.end }),
        ],
      });
      expect(sidebar.find((item) => item.id === 'user-1')!.warningGroups).toBeUndefined();
      expect(sidebar.find((item) => item.id === 'user-2')!.warningGroups).toBeUndefined();
      expect(leaveRequests).toHaveLength(1);
    });

    it('queries only displayed, filtered employees for both contributions', async () => {
      const query = { ...assignmentContext, filters: { ...context.filters, userIds: ['user-2', borrowedId, 'other'] } };
      const home = await calendarSchedulingEventsContribution.load(query, { runtimeContext: loanOutRuntime });
      const borrowed = await calendarSchedulingLoanOutContribution.load(query, { runtimeContext: loanOutRuntime });
      expect(home.resources?.map((user) => user.id)).toEqual(['user-2']);
      expect(borrowed.resources?.map((user) => user.id)).toEqual([borrowedId]);
      expect(leaveRequests.map((request) => request.userIds)).toEqual([['user-2'], [borrowedId]]);
      expect(leaveRequests.every((request) => !('locationId' in request))).toBe(true);
      expect(schedulingRequests[0]).toMatchObject({ locationId: 1, userIds: query.filters.userIds });
    });

    it.each([
      {
        name: 'missing leave permission',
        access: { ...loanOutRuntime, permissions: [Permissions.AssignmentsView, Permissions.LoanOutView] },
      },
      {
        name: 'disabled leave feature',
        access: { ...loanOutRuntime, featureFlags: { ...loanOutRuntime.featureFlags, Leave: { enabled: false } } },
      },
      {
        name: 'missing leave feature',
        access: { ...loanOutRuntime, featureFlags: { Scheduling: { enabled: true }, LoanOut: { enabled: true } } },
      },
      { name: 'missing runtime', access: undefined },
    ])('does not request warning leave with $name', async ({ access }) => {
      await calendarSchedulingEventsContribution.load(assignmentContext, { runtimeContext: access });
      await calendarSchedulingLoanOutContribution.load(assignmentContext, { runtimeContext: access });
      expect(leaveRequests).toEqual([]);
      expect(schedulingRequests).toHaveLength(1);
    });

    it('does not add a home warning request for other views or empty filtered employee lists', async () => {
      await calendarSchedulingEventsContribution.load(
        { ...assignmentContext, activeViewId: 'calendar.matrix-scheduling' },
        { runtimeContext: loanOutRuntime },
      );
      const query = { ...assignmentContext, filters: { ...context.filters, userIds: ['other'] } };
      const home = await calendarSchedulingEventsContribution.load(query, { runtimeContext: loanOutRuntime });
      const borrowed = await calendarSchedulingLoanOutContribution.load(query, { runtimeContext: loanOutRuntime });
      expect(home.resources).toEqual([]);
      expect(borrowed.resources).toEqual([]);
      expect(leaveRequests).toEqual([]);
    });

    it('does not issue unrestricted leave queries for empty teams or a missing selected location', async () => {
      server.use(
        http.get('*/api/users', () => HttpResponse.json([])),
        http.post('*/api/loan-out/scheduling/users', () => HttpResponse.json([])),
      );
      for (const query of [assignmentContext, { ...assignmentContext, locationId: undefined }]) {
        await calendarSchedulingEventsContribution.load(query, { runtimeContext: loanOutRuntime });
        await calendarSchedulingLoanOutContribution.load(query, { runtimeContext: loanOutRuntime });
      }
      expect(leaveRequests).toEqual([]);
    });

    it('keeps the primary calendar and borrowed employees available when optional leave requests fail', async () => {
      server.use(http.post('*/api/leave/calendar/events', () => new HttpResponse(null, { status: 500 })));
      const home = await calendarSchedulingEventsContribution.load(assignmentContext, {
        runtimeContext: loanOutRuntime,
      });
      const borrowed = await calendarSchedulingLoanOutContribution.load(assignmentContext, {
        runtimeContext: loanOutRuntime,
      });
      expect(home.events.map((event) => event.id)).toEqual([localLeave.id, shift.id, assignment.id]);
      expect(home.data).toEqual({ warningEvents: [] });
      expect(borrowed.events).toEqual([]);
      expect(borrowed.data).toEqual({ warningEvents: [] });
      expect(borrowed.resources?.map((user) => user.id)).toEqual([borrowedId]);
      const data = { contributions: { 'scheduling.events': home, 'scheduling.loanout': borrowed } };
      const sidebar = buildCalendarAssignmentViewModel(data, assignmentContext, 'day', loanOutRuntime).sidePanel!.items;
      expect(sidebar).toHaveLength(3);
      expect(sidebar[0]!.warningGroups?.[0]?.warnings).toEqual([expect.objectContaining({ id: localLeave.id })]);
    });
  });
});
