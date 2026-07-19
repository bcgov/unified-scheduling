import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixMetaItem as CalendarMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export interface CalendarBaseItem {
  id: string;
  type: string;
  title: string;
  subtitle?: string;
  meta?: CalendarMetaItem[];
  avatarText?: string;
}

export interface CalendarUser extends CalendarBaseItem {
  type: 'user';
}

export interface CalendarAssignment extends CalendarBaseItem {
  type: 'assignment';
  assignmentCode: string;
  capacity: number;
}

export interface CalendarEventMetadata {
  dayIndex?: number;
  shiftEntryId?: string;
  shiftSeriesId?: number;
  eventId?: number;
  userId?: string;
  userIds?: string[];
  assignmentId?: string;
  assignmentDefinitionId?: string;
  assignmentEntryId?: string;
  assignmentSeriesId?: string;
  capacity?: number;
  assignedCount?: number;
  capacitySlotStates?: CalendarAssignmentCapacitySlotState[];
  partialCoverageShifts?: CalendarAssignmentPartialCoverageShift[];
  assignedShiftIds?: string[];
  assignedUserIds?: string[];
  assignedUsers?: CalendarUser[];
  assignmentCategoryTypeId?: number;
  assignmentCategoryTypeCode?: string;
  assignmentSubCategoryTypeId?: number;
  assignmentSubCategoryTypeCode?: string;
}

export type CalendarAssignmentCapacitySlotState = 'empty' | 'filled' | 'partial';

export interface CalendarAssignmentPartialCoverageShift {
  userIds: string[];
  start?: string;
  end?: string;
  timeZoneId?: string;
}

export interface CalendarSchedulingEvent extends CalendarEventBase {
  isConflict?: boolean;
  metadata: CalendarEventMetadata;
}

export const calendarSchedulingDays = [
  { dayIndex: 0 },
  { dayIndex: 1 },
  { dayIndex: 2 },
  { dayIndex: 3 },
  { dayIndex: 4 },
  { dayIndex: 5 },
  { dayIndex: 6 },
] as const;

export function isCalendarSchedulingEvent(event: CalendarEventBase): event is CalendarSchedulingEvent {
  return 'metadata' in event && typeof event.metadata === 'object' && event.metadata !== null;
}

export function getCalendarAssignmentCapacity(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return undefined;
  }

  const capacity = event.metadata.capacity ?? 0;
  const assignedCount = event.metadata.assignedCount ?? 0;

  return {
    capacity,
    assignedCount,
    filledCount: Math.min(assignedCount, capacity),
    overflowCount: Math.max(assignedCount - capacity, 0),
  };
}

export function getCalendarAssignedUsers(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return [];
  }

  const assignedUsersById = new Map((event.metadata.assignedUsers ?? []).map((user) => [user.id, user]));

  return (event.metadata.assignedUserIds ?? []).map(
    (userId) =>
      assignedUsersById.get(userId) ?? {
        id: userId,
        type: 'user' as const,
        title: userId,
      },
  );
}
