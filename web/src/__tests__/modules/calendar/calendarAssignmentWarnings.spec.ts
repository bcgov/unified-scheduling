import { Permissions } from '@/api-access/generated/models';
import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { calendarSchedulingEventsContribution } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import type { CalendarDataResponse, CalendarQueryContext, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';
import type { CalendarSchedulingUserResource } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import type { CalendarSchedulingAssignmentResource } from '@/modules/scheduling/contributions/calendarSchedulingAssignmentResourcesContribution';
import type { TrainingCalendarEvent } from '@/modules/training/contributions/calendarTrainingEventsContribution';
import { calendarAssignmentViewContribution } from '@/modules/scheduling/calendarAssignmentViewContribution';
import { buildCalendarAssignmentViewModel, buildCalendarSchedulingViewModel } from '@/modules/scheduling/calendarSchedulingMappers';

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

function leave(id: string, start: string, end?: string, extra: Partial<CalendarSchedulingEvent> = {}): CalendarSchedulingEvent {
  return {
    id, start, end, title: id, type: 'scheduling.leave', sourceModule: 'leave',
    metadata: { userIds: ['user-1'] }, ...extra,
  };
}

function training(id: string, start: string, end: string, extra: Partial<TrainingCalendarEvent> = {}): TrainingCalendarEvent {
  return {
    id, start, end, title: id, type: 'training.user-training', sourceModule: 'training',
    userTrainingId: 1, userId: 'user-1', trainingId: 1, resourceIds: ['user-1'],
    allDay: true, locationId: 1, ...extra,
  };
}

const trainingRuntime: CalendarRuntimeContext = {
  featureFlags: { Calendar: { enabled: true }, Training: { enabled: true } },
  permissions: [...runtime.permissions!, Permissions.UserTrainingsView],
};

function response(events: CalendarSchedulingEvent[], trainingEvents: TrainingCalendarEvent[] = []): CalendarDataResponse {
  const resources: CalendarSchedulingUserResource[] = ['user-1', 'user-2'].map((id) => ({
    id, title: id, label: id, type: 'user', sourceModule: 'scheduling',
  }));
  return { contributions: { 'scheduling.events': {
    moduleId: 'scheduling', contributionId: 'scheduling.events',
    events: events.filter((event) => event.locationId == null || event.locationId === context.locationId),
    resources, data: { allLocationLeaveEvents: events },
  }, 'training.events': {
    moduleId: 'training', contributionId: 'training.events', events: trainingEvents,
  } } };
}

function items(events: CalendarSchedulingEvent[], query = context, permissions = runtime) {
  return buildCalendarAssignmentViewModel(response(events), query, 'day', permissions).sidePanel!.items;
}

describe('assignment sidebar warnings', () => {
  it('groups the existing training contribution with leave and filters training by user, location, period, and cancellation', () => {
    const data = response([leave('leave', '2025-03-09T17:00:00Z')], [
      training('later', '2025-03-09', '2025-03-11'),
      training('crosses-start', '2025-03-08', '2025-03-10'),
      training('other-location', '2025-03-09', '2025-03-10', { locationId: 2 }),
      training('no-location', '2025-03-09', '2025-03-10', { locationId: undefined }),
      training('other-user', '2025-03-09', '2025-03-10', { userId: 'other', resourceIds: ['other'] }),
      training('ends-at-start', '2025-03-08', '2025-03-09'),
      training('starts-at-end', '2025-03-10', '2025-03-11'),
      training('cancelled', '2025-03-09', '2025-03-10', { statusTypeCode: 'cancelled' }),
      training('cancelled-at', '2025-03-09', '2025-03-10', { cancelledAt: '2025-03-08T00:00:00Z' }),
    ]);
    const groups = (query = context, access = trainingRuntime) =>
      buildCalendarAssignmentViewModel(data, query, 'day', access).sidePanel!.items[0]!.warningGroups;
    expect(groups()).toEqual([
      { id: 'leave', heading: 'Leave', warnings: [expect.objectContaining({ id: 'leave' })] },
      { id: 'training', heading: 'Training', warnings: [
        expect.objectContaining({ id: 'crosses-start', start: '2025-03-08', end: '2025-03-10', allDay: true }),
        expect.objectContaining({ id: 'later' }),
      ] },
    ]);
    expect(groups({ ...context, locationId: 2 })![1]!.warnings.map((warning) => warning.id)).toEqual(['other-location']);
    expect(groups({ ...context, startDate: '2025-03-12', endDate: '2025-03-13' })).toBeUndefined();
    expect(groups(context, { ...trainingRuntime, permissions: [Permissions.UserTrainingsView] }))
      .toEqual([expect.objectContaining({ heading: 'Training' })]);
    expect(buildCalendarSchedulingViewModel(data, context, 'day').sidePanel!.items
      .every((item) => item.warningGroups === undefined)).toBe(true);
    expect(buildCalendarAssignmentViewModel(data, context, 'day', trainingRuntime)
      .sidePanel!.items[1]!.warningGroups).toBeUndefined();
  });

  it.each([
    { ...trainingRuntime, permissions: [Permissions.LeaveView] },
    { ...trainingRuntime, featureFlags: { Calendar: { enabled: true }, Training: { enabled: false } } },
    { ...trainingRuntime, featureFlags: { Calendar: { enabled: false }, Training: { enabled: true } } },
  ])('omits training warnings when its contribution is unavailable: %o', (access) => {
    const data = response([], [training('training', '2025-03-09', '2025-03-10')]);
    expect(buildCalendarAssignmentViewModel(data, context, 'day', access)
      .sidePanel!.items[0]!.warningGroups).toBeUndefined();
  });

  it('loads leave across locations in one existing bulk request while preserving regular event location filtering', async () => {
    setActivePinia(createPinia());
    const requests: Record<string, unknown>[] = [];
    server.use(
      http.get('*/api/users', () => HttpResponse.json([])),
      http.post('*/api/scheduling/calendar/events', async ({ request }) => {
        requests.push(await request.json() as Record<string, unknown>);
        const events = [
          leave('local', '2025-03-09T17:00:00Z', undefined, { locationId: 1 }),
          leave('remote', '2025-03-09T18:00:00Z', undefined, { locationId: 2 }),
          leave('global', '2025-03-09T19:00:00Z'),
          leave('remote-shift', '2025-03-09T20:00:00Z', undefined, { locationId: 2, type: 'scheduling.shift' }),
        ];
        return HttpResponse.json({ events });
      }),
    );
    const result = await calendarSchedulingEventsContribution.load({
      ...context, filters: { ...context.filters, userIds: ['user-1'] },
    });
    expect(requests).toHaveLength(1);
    expect(requests[0]).not.toHaveProperty('locationId');
    expect(requests[0]).toMatchObject({
      timeZoneId: 'America/Vancouver', startDate: context.startDate, userIds: ['user-1'],
    });
    expect(result.events.map((event) => event.id)).toEqual(['local', 'global']);
    expect(result.data).toMatchObject({ allLocationLeaveEvents: [
      expect.objectContaining({ id: 'local' }),
      expect.objectContaining({ id: 'remote' }),
      expect.objectContaining({ id: 'global' }),
    ] });
  });

  it('groups overlapping leave chronologically across locations using the calendar timezone and DST boundaries', () => {
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
    expect(user!.warningGroups).toEqual([{
      id: 'leave', heading: 'Leave', warnings: [
        expect.objectContaining({ id: 'crosses-start' }),
        expect.objectContaining({ id: 'other-location' }),
        expect.objectContaining({ id: 'overlap' }),
      ],
    }]);
    expect(emptyUser!.warningGroups).toBeUndefined();
    expect(items(events, { ...context, locationId: 2 })[0]!.warningGroups).toEqual(user!.warningGroups);
    expect(items(events, { ...context, startDate: '2025-03-12', endDate: '2025-03-13' })[0]!.warningGroups).toBeUndefined();
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
    expect(buildCalendarAssignmentViewModel({ contributions: {} }, context, 'day', runtime).sidePanel?.items).toEqual([]);
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
      buildCalendarAssignmentViewModel(response(events), query, period, runtime)
        .sidePanel!.items[0]!.warningGroups![0]!.warnings.map((warning) => warning.id);
    expect(warnings('work-week')).toEqual(['crosses-monday', 'friday']);
    expect(warnings('week')).toEqual(['crosses-monday', 'friday', 'saturday']);
  });

  it('populates warnings through the assignment view only and respects leave access and feature flags', () => {
    const data = response([leave('leave', '2025-03-09T17:00:00Z')]);
    const assignmentResource: CalendarSchedulingAssignmentResource = {
      id: 'assignment-definition-1', type: 'assignment', sourceModule: 'scheduling',
      label: 'Room', title: 'Room', assignmentDefinitionId: 1,
    };
    data.contributions['scheduling.assignment-resources'] = {
      moduleId: 'scheduling', contributionId: 'scheduling.assignment-resources',
      events: [], resources: [assignmentResource],
    };
    const assignment = calendarAssignmentViewContribution.buildModel(data, context, runtime, 'day') as ReturnType<typeof buildCalendarAssignmentViewModel>;
    expect(assignment.sidePanel!.items[0]!.warningGroups).toHaveLength(1);
    const schedule = buildCalendarSchedulingViewModel(data, context, 'day');
    expect(schedule.sidePanel!.items).toHaveLength(1);
    expect(schedule.sidePanel!.items.every((item) => item.warningGroups === undefined)).toBe(true);
    expect(schedule.primaryColumn.resources.every((resource) => !('warningGroups' in resource))).toBe(true);
    expect(items(data.contributions['scheduling.events']!.events as CalendarSchedulingEvent[], context,
      { ...runtime, permissions: [Permissions.AssignmentsView] })[0]!.warningGroups).toBeUndefined();
    expect(items([leave('leave', '2025-03-09T17:00:00Z')], context,
      { ...runtime, featureFlags: { Leave: { enabled: false } } })[0]!.warningGroups).toBeUndefined();
  });
});
