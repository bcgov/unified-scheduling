import { Permissions, type Permissions as Permission } from '@/api-access/generated/models';
import type { CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';

export function hasCalendarPermission(context: CalendarRuntimeContext, permission: Permission) {
  return context.permissions?.includes(permission) ?? false;
}

export function canViewAssignments(context: CalendarRuntimeContext) {
  return hasCalendarPermission(context, Permissions.AssignmentsView);
}

export function canViewShifts(context: CalendarRuntimeContext) {
  return hasCalendarPermission(context, Permissions.ShiftsView);
}

export function canCreateAssignments(context: CalendarRuntimeContext) {
  return hasCalendarPermission(context, Permissions.AssignmentsCreate);
}

export function canAssignAssignments(context: CalendarRuntimeContext) {
  return hasCalendarPermission(context, Permissions.AssignmentsAssign);
}

export function canEditAssignments(context: CalendarRuntimeContext) {
  return hasCalendarPermission(context, Permissions.AssignmentsEdit);
}

export function canCreateShifts(context: CalendarRuntimeContext) {
  return hasCalendarPermission(context, Permissions.ShiftsCreateAndAssign);
}

export function canEditShifts(context: CalendarRuntimeContext) {
  return hasCalendarPermission(context, Permissions.ShiftsEdit);
}

const isLeaveEnabled = (context: CalendarRuntimeContext) => context.featureFlags?.Leave?.enabled ?? true;

export function canViewLeave(context: CalendarRuntimeContext) {
  return isLeaveEnabled(context) && hasCalendarPermission(context, Permissions.LeaveView);
}

export function canCreateLeave(context: CalendarRuntimeContext) {
  return isLeaveEnabled(context) && hasCalendarPermission(context, Permissions.LeaveCreateAndAssign);
}

export function canEditLeave(context: CalendarRuntimeContext) {
  return isLeaveEnabled(context) && hasCalendarPermission(context, Permissions.LeaveEdit);
}

export function canDeleteLeave(context: CalendarRuntimeContext) {
  return isLeaveEnabled(context) && hasCalendarPermission(context, Permissions.LeaveDelete);
}
