import {
  canAddAssignmentLinkToShift,
  getSchedulingLifecycleCapabilities,
  normalizeSchedulingLifecycleStatus,
} from '@/modules/scheduling/schedulingLifecycle';
import { describe, expect, it } from 'vitest';

describe('schedulingLifecycle', () => {
  it.each([
    ['Draft', 'draft', true, true, false],
    ['Active', 'published', false, false, true],
    ['Published', 'published', false, false, true],
    ['Cancelled', 'cancelled', false, false, false],
  ] as const)('maps %s to its assignment capabilities', (code, status, canEdit, canDelete, canCancel) => {
    expect(normalizeSchedulingLifecycleStatus(code)).toBe(status);
    expect(getSchedulingLifecycleCapabilities(code)).toEqual({ status, canEdit, canDelete, canCancel });
  });

  it('allows new Assignment links only to draft Shift entries and series', () => {
    expect(canAddAssignmentLinkToShift('Draft')).toBe(true);
    expect(canAddAssignmentLinkToShift('Active')).toBe(false);
    expect(canAddAssignmentLinkToShift('Cancelled')).toBe(false);
  });
});
