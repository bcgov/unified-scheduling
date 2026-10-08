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

const isTimeOffEnabled = (context: CalendarRuntimeContext) => context.featureFlags?.TimeOff?.enabled ?? true;

export function canViewTimeOff(context: CalendarRuntimeContext) {
  return isTimeOffEnabled(context) && hasCalendarPermission(context, Permissions.TimeOffView);
}

export function canCreateTimeOff(context: CalendarRuntimeContext) {
  return isTimeOffEnabled(context) && hasCalendarPermission(context, Permissions.TimeOffCreateAndAssign);
}

export function canEditTimeOff(context: CalendarRuntimeContext) {
  return isTimeOffEnabled(context) && hasCalendarPermission(context, Permissions.TimeOffEdit);
}

export function canDeleteTimeOff(context: CalendarRuntimeContext) {
  return isTimeOffEnabled(context) && hasCalendarPermission(context, Permissions.TimeOffDelete);
}
