import { beforeEach, describe, expect, it, vi } from 'vitest';

function createFetchResult<T>(value: T) {
  return {
    data: { value },
    error: { value: null },
    execute: vi.fn().mockResolvedValue(undefined),
  };
}

describe('calendarSchedulingAssignmentResourcesContribution', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('does not fetch without an active location', async () => {
    const getApiSchedulingAssignmentDefinitions = vi.fn();
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions,
    }));

    const { calendarSchedulingAssignmentResourcesContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingAssignmentResourcesContribution');
    const result = await calendarSchedulingAssignmentResourcesContribution.load({
      startDate: '2026-07-13',
      endDate: '2026-07-18',
      filters: {},
    });

    expect(getApiSchedulingAssignmentDefinitions).not.toHaveBeenCalled();
    expect(result.events).toEqual([]);
    expect(result.resources).toEqual([]);
  });

  it('provides active assignment-definition resources without fetching assignment events', async () => {
    const getApiSchedulingAssignmentDefinitions = vi.fn().mockReturnValue(
      createFetchResult([
        {
          id: 1,
          locationId: 12,
          name: 'Expired before range',
          effectiveDateUtc: '2026-06-01T00:00:00Z',
          expiryDateUtc: '2026-07-12T07:00:00Z',
        },
        {
          id: 2,
          locationId: 12,
          name: 'Court coverage',
          effectiveDateUtc: '2026-07-13T07:00:00Z',
          expiryDateUtc: null,
        },
      ]),
    );
    vi.doMock('@/api-access/generated/assignment-definition/assignment-definition', () => ({
      getApiSchedulingAssignmentDefinitions,
    }));

    const { calendarSchedulingAssignmentResourcesContribution } =
      await import('@/modules/scheduling/contributions/calendarSchedulingAssignmentResourcesContribution');
    const result = await calendarSchedulingAssignmentResourcesContribution.load({
      startDate: '2026-07-13',
      endDate: '2026-07-18',
      locationId: 12,
      filters: { timeZoneId: 'America/Vancouver' },
    });

    expect(getApiSchedulingAssignmentDefinitions).toHaveBeenCalledWith(
      { locationId: 12 },
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(result.events).toEqual([]);
    expect(result.resources?.map((resource) => resource.label)).toEqual(['Court coverage']);
  });
});
