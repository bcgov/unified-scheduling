import {
  canAddAssignmentLinkToShift,
  getSchedulingLifecycleCapabilities,
  normalizeSchedulingLifecycleStatus,
} from '@/modules/scheduling/schedulingLifecycle';
import { describe, expect, it } from 'vitest';

describe('schedulingLifecycle', () => {
  it.each([
    ['Draft', 'draft', true, true, true, false],
    ['Active', 'published', true, false, false, true],
    ['Published', 'published', true, false, false, true],
    ['Cancelled', 'cancelled', false, false, false, false],
    ['other', 'unknown', false, false, false, false],
  ] as const)(
    'maps %s to its occurrence and series capabilities',
    (code, status, canEditOccurrence, canEditSeries, canDelete, canCancel) => {
      expect(normalizeSchedulingLifecycleStatus(code)).toBe(status);
      expect(getSchedulingLifecycleCapabilities(code)).toEqual({
        status,
        canEditOccurrence,
        canEditSeries,
        canDelete,
        canCancel,
      });
    },
  );

  it('allows links to published shift occurrences and only draft shift series', () => {
    expect(canAddAssignmentLinkToShift('Draft', 'entry')).toBe(true);
    expect(canAddAssignmentLinkToShift('Active', 'entry')).toBe(true);
    expect(canAddAssignmentLinkToShift('Cancelled', 'entry')).toBe(false);
    expect(canAddAssignmentLinkToShift('Draft', 'series')).toBe(true);
    expect(canAddAssignmentLinkToShift('Active', 'series')).toBe(false);
  });
});
