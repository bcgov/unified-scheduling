import { describe, expect, it } from 'vitest';
import { Permissions } from '@/api-access/generated/models';
import {
  canCreateAssignments,
  canEditAssignments,
  canViewAssignments,
} from '@/modules/scheduling/calendarSchedulingPermissions';

describe('calendar scheduling permissions', () => {
  it('keeps view and write capabilities distinct', () => {
    const context = { featureFlags: {}, permissions: [Permissions.AssignmentsView] };
    expect(canViewAssignments(context)).toBe(true);
    expect(canCreateAssignments(context)).toBe(false);
    expect(canEditAssignments(context)).toBe(false);
  });

  it('fails closed while permissions are unavailable', () => {
    expect(canViewAssignments({ featureFlags: {} })).toBe(false);
    expect(canCreateAssignments({ featureFlags: {} })).toBe(false);
  });
});
