import { beforeEach, describe, expect, it, vi } from 'vitest';

function createFetchResult<T>(value: T) {
  return {
    data: { value },
    error: { value: null },
    execute: vi.fn().mockResolvedValue(undefined),
  };
}

describe('calendarSchedulingAssignmentsContribution', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('fails closed and does not fetch assignments or definitions without an active location', async () => {
    const getApiSchedulingAssignmentsEntries = vi.fn();
    const getApiSchedulingAssignmentDefinitions = vi.fn();

    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntries,
    }));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions,
    }));

    const { calendarSchedulingAssignmentsContribution } = await import(
      '@/modules/scheduling/contributions/calendarSchedulingAssignmentsContribution'
    );

    const result = await calendarSchedulingAssignmentsContribution.load({
      startDate: '2025-04-07',
      endDate: '2025-04-14',
      filters: {},
    });

    expect(result.events).toEqual([]);
    expect(result.resources).toEqual([]);
    expect(result.data).toEqual({ entries: [], definitions: [] });
    expect(getApiSchedulingAssignmentsEntries).not.toHaveBeenCalled();
    expect(getApiSchedulingAssignmentDefinitions).not.toHaveBeenCalled();
  });

  it('only maps assignment definitions valid for at least one displayed day into side panel resources', async () => {
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntries: vi.fn().mockReturnValue(createFetchResult([])),
    }));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult([
          {
            id: 1,
            locationId: 12,
            name: 'Expired before range',
            effectiveDateUtc: '2026-06-01T00:00:00Z',
            expiryDateUtc: '2026-07-13T07:00:00Z',
          },
          {
            id: 2,
            locationId: 12,
            name: 'Starts during range',
            effectiveDateUtc: '2026-07-15T07:00:00Z',
            expiryDateUtc: null,
          },
          {
            id: 3,
            locationId: 12,
            name: 'Ends during range',
            effectiveDateUtc: '2026-06-01T00:00:00Z',
            expiryDateUtc: '2026-07-16T07:00:00Z',
          },
          {
            id: 4,
            locationId: 12,
            name: 'Starts after range',
            effectiveDateUtc: '2026-07-20T07:00:00Z',
            expiryDateUtc: null,
          },
        ]),
      ),
    }));

    const { calendarSchedulingAssignmentsContribution } = await import(
      '@/modules/scheduling/contributions/calendarSchedulingAssignmentsContribution'
    );

    const result = await calendarSchedulingAssignmentsContribution.load({
      startDate: '2026-07-13',
      endDate: '2026-07-18',
      locationId: 12,
      filters: { timeZoneId: 'America/Vancouver' },
    });

    expect(result.resources?.map((resource) => resource.label)).toEqual(['Ends during range', 'Starts during range']);
  });

  it('keeps assignment definition resources required by visible assignment entries even when expired', async () => {
    vi.doMock('@/api-access/generated/assignment/assignment', () => ({
      getApiSchedulingAssignmentsEntries: vi.fn().mockReturnValue(
        createFetchResult([
          {
            id: 99,
            assignmentDefinitionId: 1,
            title: 'Visible expired-definition assignment',
            startAtUtc: '2026-07-14T16:00:00Z',
            endAtUtc: '2026-07-15T00:00:00Z',
            assignedUserIds: [],
            linkedShiftEntryIds: [],
          },
        ]),
      ),
    }));

    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions: vi.fn().mockReturnValue(
        createFetchResult([
          {
            id: 1,
            locationId: 12,
            name: 'Expired but visible',
            effectiveDateUtc: '2026-06-01T00:00:00Z',
            expiryDateUtc: '2026-07-01T00:00:00Z',
          },
          {
            id: 2,
            locationId: 12,
            name: 'Expired and unused',
            effectiveDateUtc: '2026-06-01T00:00:00Z',
            expiryDateUtc: '2026-07-01T00:00:00Z',
          },
        ]),
      ),
    }));

    const { calendarSchedulingAssignmentsContribution } = await import(
      '@/modules/scheduling/contributions/calendarSchedulingAssignmentsContribution'
    );

    const result = await calendarSchedulingAssignmentsContribution.load({
      startDate: '2026-07-13',
      endDate: '2026-07-18',
      locationId: 12,
      filters: { timeZoneId: 'America/Vancouver' },
    });

    expect(result.resources?.map((resource) => resource.label)).toEqual(['Expired but visible']);
    expect(result.events.map((event) => event.title)).toEqual(['Visible expired-definition assignment']);
  });
});
