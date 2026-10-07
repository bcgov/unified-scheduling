import { describe, expect, it } from 'vitest';
import { Permissions } from '@/api-access/generated/models';
import {
  canCreateAssignments,
  canEditAssignments,
  canEditShifts,
  canPublishSchedule,
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

  it('keeps schedule publishing distinct from ordinary edit permissions', () => {
    const publishContext = { featureFlags: {}, permissions: [Permissions.SchedulePublish] };
    expect(canPublishSchedule(publishContext)).toBe(true);
    expect(canEditAssignments(publishContext)).toBe(false);
    expect(canEditShifts(publishContext)).toBe(false);

    const editContext = {
      featureFlags: {},
      permissions: [Permissions.AssignmentsEdit, Permissions.ShiftsEdit],
    };
    expect(canPublishSchedule(editContext)).toBe(false);
  });
});
