import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import {
  assignmentDefinitionOverlapsCalendarDateRange,
  fromUtcBusinessDateInput,
  isAssignmentDefinitionSelectableForAssignmentDate,
  toUtcBusinessDateInput,
} from '@/modules/scheduling/assignmentDefinitionDateHelpers';
import { DateTime } from 'luxon';
import { describe, expect, it } from 'vitest';

const definition = (overrides: Partial<AssignmentDefinitionResponse> = {}): AssignmentDefinitionResponse => ({
  id: 1,
  effectiveDateUtc: '2026-08-01T00:00:00Z',
  expiryDateUtc: '2026-09-01T00:00:00Z',
  ...overrides,
});

describe('assignmentDefinitionDateHelpers', () => {
  it('round-trips UTC business dates without applying the location timezone', () => {
    expect(toUtcBusinessDateInput('2026-08-01T00:00:00Z')).toBe('2026-08-01');
    expect(fromUtcBusinessDateInput('2026-08-01')).toBe('2026-08-01T00:00:00Z');
    expect(toUtcBusinessDateInput(fromUtcBusinessDateInput('2026-08-01'))).toBe('2026-08-01');
  });

  it('preserves a business date near a DST transition regardless of browser or location zone', () => {
    const utcValue = fromUtcBusinessDateInput('2026-11-01');

    expect(utcValue).toBe('2026-11-01T00:00:00Z');
    expect(toUtcBusinessDateInput(utcValue)).toBe('2026-11-01');
    expect(DateTime.fromISO(utcValue!, { zone: 'America/Toronto' }).isValid).toBe(true);
  });

  it('treats expiry as an exclusive business-date boundary', () => {
    const beforeExpiry = DateTime.fromISO('2026-08-31', { zone: 'America/Vancouver' });
    const atExpiry = DateTime.fromISO('2026-09-01', { zone: 'America/Vancouver' });

    expect(isAssignmentDefinitionSelectableForAssignmentDate(definition(), beforeExpiry, 'America/Vancouver')).toBe(
      true,
    );
    expect(isAssignmentDefinitionSelectableForAssignmentDate(definition(), atExpiry, 'America/Vancouver')).toBe(false);
  });

  it('uses the Calendar exclusive-end range when checking overlap', () => {
    expect(
      assignmentDefinitionOverlapsCalendarDateRange(
        definition({ effectiveDateUtc: '2026-08-10T00:00:00Z', expiryDateUtc: null }),
        '2026-08-03',
        '2026-08-10',
        'America/Vancouver',
      ),
    ).toBe(false);
  });
});
