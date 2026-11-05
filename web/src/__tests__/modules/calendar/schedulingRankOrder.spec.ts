import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '../../mocks/server';
import { LookupCodeTypes, type UserResponse } from '@/api-access/generated/models';
import { useLookupStore } from '@/stores/LookupStore';
import { sortSchedulingUsers } from '@/modules/scheduling/sortSchedulingUsers';
import { calendarSchedulingEventsContribution } from '@/modules/scheduling/contributions/calendarSchedulingEventsContribution';
import {
  buildCalendarAssignmentViewModel,
  buildCalendarSchedulingViewModel,
} from '@/modules/scheduling/calendarSchedulingMappers';

const ranks = [
  'Chief Sheriff',
  'Deputy Chief Sheriff',
  'Superintendent',
  'Senior Inspector',
  'Staff Inspector',
  'Inspector',
  'Staff Sergeant',
  'Sergeant',
  'Deputy Sheriff',
];
const positions = ranks.map((code, index) => ({ code, description: code, displayOrder: index + 1 }));
const user = (id: string, rank: string | null, lastName = 'Smith', firstName = 'Alex'): UserResponse => ({
  id,
  rank,
  lastName,
  firstName,
  idirName: id,
  isEnabled: true,
  email: '',
  gender: 'Other',
  employeeNumber: '',
});
const context = { startDate: '2026-07-13', endDate: '2026-07-14', locationId: 12, filters: {} };

beforeEach(() => setActivePinia(createPinia()));

describe('scheduling rank order', () => {
  it('sorts every rank, then case-insensitive last and first names and ID without mutating the input', () => {
    const users = [
      user('unknown', 'Unknown', 'A'),
      user('missing', null, 'Z'),
      ...ranks.map((rank, index) => user(String(index), rank)).reverse(),
      user('b', 'Sergeant', 'alpha', 'Sam'),
      user('a', 'Sergeant', 'Alpha', 'sam'),
      user('c', 'Sergeant', 'ALPHA', 'Zoe'),
      user('d', 'Sergeant', 'Beta', 'Aaron'),
      user('empty', '', '', ''),
    ];
    const original = [...users];
    const byCode = Object.fromEntries(positions.map((position) => [position.code, position]));
    expect(sortSchedulingUsers(users, byCode).map((item) => item.id)).toEqual([
      '0',
      '1',
      '2',
      '3',
      '4',
      '5',
      '6',
      'a',
      'b',
      'c',
      'd',
      '7',
      '8',
      'empty',
      'unknown',
      'missing',
    ]);
    expect(users).toEqual(original);
    expect(sortSchedulingUsers([], byCode)).toEqual([]);
  });

  it('loads once and supplies the same ordered users to both views across refreshes and filters', async () => {
    let lookupRequests = 0;
    const users = [
      user('deputy', 'Deputy Sheriff', 'Adams'),
      user('chief', 'Chief Sheriff', 'Zulu'),
      user('unknown', null, 'Beta'),
    ];
    server.use(
      http.get('*/api/lookup/PositionTypes', () => {
        lookupRequests++;
        return HttpResponse.json(positions);
      }),
      http.get('*/api/users', () => HttpResponse.json(users)),
      http.get('*/api/users/:id/acting-positions', () => HttpResponse.json([])),
      http.post('*/api/scheduling/calendar/events', () => HttpResponse.json({ events: [] })),
    );
    const contribution = await calendarSchedulingEventsContribution.load(context);
    const refreshed = await calendarSchedulingEventsContribution.load(context);
    const filtered = await calendarSchedulingEventsContribution.load({
      ...context,
      filters: { userIds: ['deputy', 'chief'] },
    });
    expect(contribution.resources?.map((item) => item.id)).toEqual(['chief', 'deputy', 'unknown']);
    expect(refreshed.resources).toEqual(contribution.resources);
    expect(filtered.resources?.map((item) => item.id)).toEqual(['chief', 'deputy']);
    expect(lookupRequests).toBe(1);

    const data = { contributions: { 'scheduling.events': contribution } };
    const schedule = buildCalendarSchedulingViewModel(data, context, 'day');
    const assignment = buildCalendarAssignmentViewModel(data, context, 'day');
    expect(schedule.primaryColumn.resources.map((item) => item.id)).toEqual(['chief', 'deputy', 'unknown']);
    expect(assignment.sidePanel?.items.map((item) => item.id)).toEqual(['chief', 'deputy', 'unknown']);
    expect(assignment.sidePanel?.items.map((item) => item.payload)).toEqual([
      { userId: 'chief', title: 'Alex Zulu', sourceModule: 'scheduling', loanOutMetaData: undefined },
      { userId: 'deputy', title: 'Alex Adams', sourceModule: 'scheduling', loanOutMetaData: undefined },
      { userId: 'unknown', title: 'Alex Beta', sourceModule: 'scheduling', loanOutMetaData: undefined },
    ]);
    expect(assignment.sidePanel?.items.every((item) => item.draggable)).toBe(true);
  });

  it('waits for a concurrent lookup request and retries after a failure', async () => {
    let release!: () => void;
    let started!: () => void;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const requestStarted = new Promise<void>((resolve) => {
      started = resolve;
    });
    let calls = 0;
    server.use(
      http.get('*/api/lookup/PositionTypes', async () => {
        calls++;
        started();
        await gate;
        return HttpResponse.json(positions);
      }),
    );
    const store = useLookupStore();
    const first = store.load(LookupCodeTypes.PositionTypes);
    const second = store.load(LookupCodeTypes.PositionTypes);
    let secondCompleted = false;
    void second.then(() => {
      secondCompleted = true;
    });
    await requestStarted;
    expect(secondCompleted).toBe(false);
    release();
    await Promise.all([first, second]);
    expect(calls).toBe(1);
    expect(store.entityMap.PositionTypes?.['Chief Sheriff']?.displayOrder).toBe(1);

    setActivePinia(createPinia());
    server.use(http.get('*/api/lookup/PositionTypes', () => new HttpResponse(null, { status: 500 })));
    const retryStore = useLookupStore();
    await expect(retryStore.load(LookupCodeTypes.PositionTypes)).rejects.toBeTruthy();
    server.use(http.get('*/api/lookup/PositionTypes', () => HttpResponse.json(positions)));
    await retryStore.load(LookupCodeTypes.PositionTypes);
    expect(retryStore.entityMap.PositionTypes?.Sergeant?.displayOrder).toBe(8);
  });

  it('keeps the existing option ordering for other lookups', async () => {
    server.use(
      http.get('*/api/lookup/EventTypes', () =>
        HttpResponse.json([
          { code: 'Z', description: 'Zulu' },
          { code: 'A', description: 'Alpha' },
        ]),
      ),
    );
    const store = useLookupStore();
    await store.load(LookupCodeTypes.EventTypes);
    expect(store.getSelectOptions(LookupCodeTypes.EventTypes).map((item) => item.code)).toEqual(['A', 'Z']);
  });

  it('does not publish resources for an aborted calendar request', async () => {
    const controller = new AbortController();
    controller.abort();
    server.use(
      http.get('*/api/lookup/PositionTypes', () => HttpResponse.json(positions)),
      http.get('*/api/users', () => HttpResponse.json([])),
      http.post('*/api/scheduling/calendar/events', () => HttpResponse.json({ events: [] })),
    );
    await expect(
      calendarSchedulingEventsContribution.load(context, { signal: controller.signal }),
    ).rejects.toBeTruthy();
  });

  it('fails a calendar load if the position lookup has no display orders', async () => {
    server.use(
      http.get('*/api/lookup/PositionTypes', () => HttpResponse.json([{ code: 'Chief Sheriff' }])),
      http.get('*/api/users', () => HttpResponse.json([])),
      http.post('*/api/scheduling/calendar/events', () => HttpResponse.json({ events: [] })),
    );
    await expect(calendarSchedulingEventsContribution.load(context)).rejects.toThrow(
      'Position type display order is unavailable',
    );
  });

  it('recovers from a partially invalid position lookup without resetting the store', async () => {
    let calls = 0;
    server.use(
      http.get('*/api/lookup/PositionTypes', () => {
        calls++;
        return HttpResponse.json(calls === 1 ? [positions[0], { code: 'Sergeant' }] : positions);
      }),
      http.get('*/api/users', () =>
        HttpResponse.json([
          user('unknown', 'Retired', 'Alpha'),
          user('sergeant', 'Sergeant', 'Zulu'),
          user('chief', 'Chief Sheriff', 'Zulu'),
        ]),
      ),
      http.get('*/api/users/:id/acting-positions', () => HttpResponse.json([])),
      http.post('*/api/scheduling/calendar/events', () => HttpResponse.json({ events: [] })),
    );
    await expect(calendarSchedulingEventsContribution.load(context)).rejects.toThrow('Position type display order');
    const recovered = await calendarSchedulingEventsContribution.load(context);
    expect(recovered.resources?.map((item) => item.id)).toEqual(['chief', 'sergeant', 'unknown']);
    expect(calls).toBe(2);
  });

  it('preserves assignment rows, shift associations and the trailing unassigned row', () => {
    const response = {
      contributions: {
        'scheduling.events': {
          moduleId: 'scheduling',
          contributionId: 'scheduling.events',
          resources: [
            { id: 'chief', type: 'user', sourceModule: 'scheduling', label: 'Chief', title: 'Chief' },
            { id: 'deputy', type: 'user', sourceModule: 'scheduling', label: 'Deputy', title: 'Deputy' },
          ],
          events: [
            {
              id: 'assigned',
              type: 'scheduling.shift',
              sourceModule: 'scheduling',
              title: 'Assigned',
              start: '2026-07-13T16:00:00Z',
              resourceIds: ['deputy'],
              metadata: { userIds: ['deputy'] },
            },
            {
              id: 'unassigned',
              type: 'scheduling.shift',
              sourceModule: 'scheduling',
              title: 'Unassigned',
              start: '2026-07-13T16:00:00Z',
              resourceIds: [],
              metadata: { userIds: [] },
            },
          ],
        },
        'scheduling.assignment-resources': {
          moduleId: 'scheduling',
          contributionId: 'scheduling.assignment-resources',
          events: [],
          resources: [
            {
              id: 'assignment-definition-2',
              type: 'assignment',
              sourceModule: 'scheduling',
              title: 'First',
              label: 'First',
              assignmentDefinitionId: 2,
            },
            {
              id: 'assignment-definition-1',
              type: 'assignment',
              sourceModule: 'scheduling',
              title: 'Second',
              label: 'Second',
              assignmentDefinitionId: 1,
            },
          ],
        },
      },
    };
    const schedule = buildCalendarSchedulingViewModel(response, context, 'day');
    const assignment = buildCalendarAssignmentViewModel(response, context, 'day');
    expect(schedule.primaryColumn.resources.map((item) => item.id)).toEqual([
      'chief',
      'deputy',
      'scheduling.unassigned',
    ]);
    expect(schedule.cells.find((cell) => cell.resourceId === 'deputy')?.headers?.map((item) => item.id)).toEqual([
      'assigned',
    ]);
    expect(
      schedule.cells.find((cell) => cell.resourceId === 'scheduling.unassigned')?.headers?.map((item) => item.id),
    ).toEqual(['unassigned']);
    expect(assignment.primaryColumn.resources.map((item) => item.id)).toEqual([
      'assignment-definition-2',
      'assignment-definition-1',
    ]);
  });
});
