import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixMetaItem as CalendarMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { calendarMatrixColorMap, type CalendarMatrixColorId } from './calendarSchedulingColors';

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
  colorId: CalendarMatrixColorId;
}

type CalendarSchedulingEventStatus = 'active' | 'draft';

export interface CalendarShiftEntry {
  id: string;
  userId: string;
  dayIndex: number;
  title: string;
  startTime: string;
  endTime: string;
  status: CalendarSchedulingEventStatus;
}

export interface CalendarAssignmentEntry {
  id: string;
  assignmentId: string;
  dayIndex: number;
  title: string;
  startTime: string;
  endTime: string;
  status: CalendarSchedulingEventStatus;
  capacity: number;
  colorId: CalendarMatrixColorId;
}

export interface CalendarShiftAssignmentEntry {
  id: string;
  shiftEntryId: string;
  assignmentEntryId: string;
  eventId: string;
}

export interface CalendarEventMetadata {
  dayIndex?: number;
  shiftEntryId?: string;
  shiftSeriesId?: number;
  eventId?: number;
  userId?: string;
  userIds?: string[];
  assignmentId?: string;
  assignmentEntryId?: string;
  capacity?: number;
  assignedCount?: number;
  assignedShiftIds?: string[];
  assignedUserIds?: string[];
}

export interface CalendarSchedulingEvent extends CalendarEventBase {
  isConflict?: boolean;
  metadata: CalendarEventMetadata;
}

export const calendarSchedulingUsers: CalendarUser[] = [
  {
    id: 'scheduling-user-avery',
    type: 'user',
    title: 'Avery Chen',
    subtitle: 'Senior coordinator',
    meta: [{ label: 'Availability', value: 'Full time' }],
    avatarText: 'AC',
  },
  {
    id: 'scheduling-user-jordan',
    type: 'user',
    title: 'Jordan Patel',
    subtitle: 'Program lead',
    meta: [{ label: 'Availability', value: 'Full time' }],
    avatarText: 'JP',
  },
  {
    id: 'scheduling-user-morgan',
    type: 'user',
    title: 'Morgan Lee',
    subtitle: 'Floater',
    meta: [{ label: 'Availability', value: 'Part time' }],
    avatarText: 'ML',
  },
  {
    id: 'scheduling-user-riley',
    type: 'user',
    title: 'Riley Smith',
    subtitle: 'Coverage support',
    meta: [{ label: 'Availability', value: 'On call' }],
    avatarText: 'RS',
  },
];

export const calendarSchedulingDays = [
  { dayIndex: 0 },
  { dayIndex: 1 },
  { dayIndex: 2 },
  { dayIndex: 3 },
  { dayIndex: 4 },
  { dayIndex: 5 },
  { dayIndex: 6 },
] as const;

export const calendarAssignments: CalendarAssignment[] = [
  {
    id: 'scheduling-assignment-intake',
    type: 'assignment',
    title: 'Intake desk',
    subtitle: 'Front counter',
    meta: [{ label: '', value: '2 openings' }],
    avatarText: 'ID',
    assignmentCode: 'INTAKE',
    capacity: 2,
    colorId: 'blue',
  },
  {
    id: 'scheduling-assignment-outreach',
    type: 'assignment',
    title: 'Outreach calls',
    subtitle: 'Client follow-up',
    meta: [{ label: 'Status', value: 'Draft coverage' }],
    avatarText: 'OC',
    assignmentCode: 'OUTREACH',
    capacity: 3,
    colorId: 'orange',
  },
  {
    id: 'scheduling-assignment-triage',
    type: 'assignment',
    title: 'Triage support',
    subtitle: 'Same-day queue',
    meta: [{ label: '', value: 'Priority' }],
    avatarText: 'TS',
    assignmentCode: 'TRIAGE',
    capacity: 2,
    colorId: 'purple',
  },
  {
    id: 'scheduling-assignment-admin',
    type: 'assignment',
    title: 'Admin block',
    subtitle: 'Documentation',
    meta: [{ label: 'Schedule', value: 'Flexible' }],
    avatarText: 'AB',
    assignmentCode: 'ADMIN',
    capacity: 1,
    colorId: 'teal',
  },
];

export const calendarShiftEntries: CalendarShiftEntry[] = [
  {
    id: 'scheduling-shift-avery-mon',
    userId: 'scheduling-user-avery',
    dayIndex: 0,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'scheduling-shift-avery-tue',
    userId: 'scheduling-user-avery',
    dayIndex: 1,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-avery-wed',
    userId: 'scheduling-user-avery',
    dayIndex: 2,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-avery-fri',
    userId: 'scheduling-user-avery',
    dayIndex: 4,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-jordan-mon',
    userId: 'scheduling-user-jordan',
    dayIndex: 0,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-jordan-tue',
    userId: 'scheduling-user-jordan',
    dayIndex: 1,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'scheduling-shift-jordan-thu',
    userId: 'scheduling-user-jordan',
    dayIndex: 3,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-jordan-fri',
    userId: 'scheduling-user-jordan',
    dayIndex: 4,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-morgan-tue',
    userId: 'scheduling-user-morgan',
    dayIndex: 1,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-morgan-wed',
    userId: 'scheduling-user-morgan',
    dayIndex: 2,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'scheduling-shift-morgan-thu',
    userId: 'scheduling-user-morgan',
    dayIndex: 3,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-morgan-sat',
    userId: 'scheduling-user-morgan',
    dayIndex: 5,
    title: 'Weekend shift',
    startTime: '10:00:00',
    endTime: '14:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-riley-mon',
    userId: 'scheduling-user-riley',
    dayIndex: 0,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-riley-wed',
    userId: 'scheduling-user-riley',
    dayIndex: 2,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'scheduling-shift-riley-thu',
    userId: 'scheduling-user-riley',
    dayIndex: 3,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'scheduling-shift-riley-sun',
    userId: 'scheduling-user-riley',
    dayIndex: 6,
    title: 'Weekend shift',
    startTime: '11:00:00',
    endTime: '15:00:00',
    status: 'active',
  },
];

export const calendarShiftAssignments: CalendarShiftAssignmentEntry[] = [
  {
    id: 'scheduling-shift-assignment-avery-mon-intake',
    shiftEntryId: 'scheduling-shift-avery-mon',
    assignmentEntryId: 'scheduling-assignment-entry-intake-day-0',
    eventId: 'scheduling-shift-assignment-event-avery-mon-intake',
  },
  {
    id: 'scheduling-shift-assignment-avery-mon-outreach',
    shiftEntryId: 'scheduling-shift-avery-mon',
    assignmentEntryId: 'scheduling-assignment-entry-outreach-day-0',
    eventId: 'scheduling-shift-assignment-event-avery-mon-outreach',
  },
  {
    id: 'scheduling-shift-assignment-jordan-mon-triage',
    shiftEntryId: 'scheduling-shift-jordan-mon',
    assignmentEntryId: 'scheduling-assignment-entry-triage-day-0',
    eventId: 'scheduling-shift-assignment-event-jordan-mon-triage',
  },
  {
    id: 'scheduling-shift-assignment-jordan-mon-intake',
    shiftEntryId: 'scheduling-shift-jordan-mon',
    assignmentEntryId: 'scheduling-assignment-entry-intake-day-0',
    eventId: 'scheduling-shift-assignment-event-jordan-mon-intake',
  },
  {
    id: 'scheduling-shift-assignment-riley-mon-intake',
    shiftEntryId: 'scheduling-shift-riley-mon',
    assignmentEntryId: 'scheduling-assignment-entry-intake-day-0',
    eventId: 'scheduling-shift-assignment-event-riley-mon-intake',
  },
  {
    id: 'scheduling-shift-assignment-avery-tue-outreach',
    shiftEntryId: 'scheduling-shift-avery-tue',
    assignmentEntryId: 'scheduling-assignment-entry-outreach-day-1',
    eventId: 'scheduling-shift-assignment-event-avery-tue-outreach',
  },
  {
    id: 'scheduling-shift-assignment-jordan-tue-admin',
    shiftEntryId: 'scheduling-shift-jordan-tue',
    assignmentEntryId: 'scheduling-assignment-entry-admin-day-1',
    eventId: 'scheduling-shift-assignment-event-jordan-tue-admin',
  },
  {
    id: 'scheduling-shift-assignment-jordan-tue-outreach',
    shiftEntryId: 'scheduling-shift-jordan-tue',
    assignmentEntryId: 'scheduling-assignment-entry-outreach-day-1',
    eventId: 'scheduling-shift-assignment-event-jordan-tue-outreach',
  },
  {
    id: 'scheduling-shift-assignment-morgan-tue-outreach',
    shiftEntryId: 'scheduling-shift-morgan-tue',
    assignmentEntryId: 'scheduling-assignment-entry-outreach-day-1',
    eventId: 'scheduling-shift-assignment-event-morgan-tue-outreach',
  },
  {
    id: 'scheduling-shift-assignment-avery-wed-intake',
    shiftEntryId: 'scheduling-shift-avery-wed',
    assignmentEntryId: 'scheduling-assignment-entry-intake-day-2',
    eventId: 'scheduling-shift-assignment-event-avery-wed-intake',
  },
  {
    id: 'scheduling-shift-assignment-morgan-wed-intake',
    shiftEntryId: 'scheduling-shift-morgan-wed',
    assignmentEntryId: 'scheduling-assignment-entry-intake-day-2',
    eventId: 'scheduling-shift-assignment-event-morgan-wed-intake',
  },
  {
    id: 'scheduling-shift-assignment-riley-wed-triage',
    shiftEntryId: 'scheduling-shift-riley-wed',
    assignmentEntryId: 'scheduling-assignment-entry-triage-day-2',
    eventId: 'scheduling-shift-assignment-event-riley-wed-triage',
  },
  {
    id: 'scheduling-shift-assignment-jordan-thu-admin',
    shiftEntryId: 'scheduling-shift-jordan-thu',
    assignmentEntryId: 'scheduling-assignment-entry-admin-day-3',
    eventId: 'scheduling-shift-assignment-event-jordan-thu-admin',
  },
  {
    id: 'scheduling-shift-assignment-morgan-thu-triage',
    shiftEntryId: 'scheduling-shift-morgan-thu',
    assignmentEntryId: 'scheduling-assignment-entry-triage-day-3',
    eventId: 'scheduling-shift-assignment-event-morgan-thu-triage',
  },
  {
    id: 'scheduling-shift-assignment-riley-thu-triage',
    shiftEntryId: 'scheduling-shift-riley-thu',
    assignmentEntryId: 'scheduling-assignment-entry-triage-day-3',
    eventId: 'scheduling-shift-assignment-event-riley-thu-triage',
  },
  {
    id: 'scheduling-shift-assignment-riley-thu-admin',
    shiftEntryId: 'scheduling-shift-riley-thu',
    assignmentEntryId: 'scheduling-assignment-entry-admin-day-3',
    eventId: 'scheduling-shift-assignment-event-riley-thu-admin',
  },
  {
    id: 'scheduling-shift-assignment-avery-fri-triage',
    shiftEntryId: 'scheduling-shift-avery-fri',
    assignmentEntryId: 'scheduling-assignment-entry-triage-day-4',
    eventId: 'scheduling-shift-assignment-event-avery-fri-triage',
  },
  {
    id: 'scheduling-shift-assignment-jordan-fri-outreach',
    shiftEntryId: 'scheduling-shift-jordan-fri',
    assignmentEntryId: 'scheduling-assignment-entry-outreach-day-4',
    eventId: 'scheduling-shift-assignment-event-jordan-fri-outreach',
  },
  {
    id: 'scheduling-shift-assignment-morgan-sat-intake',
    shiftEntryId: 'scheduling-shift-morgan-sat',
    assignmentEntryId: 'scheduling-assignment-entry-intake-day-5',
    eventId: 'scheduling-shift-assignment-event-morgan-sat-intake',
  },
  {
    id: 'scheduling-shift-assignment-riley-sun-triage',
    shiftEntryId: 'scheduling-shift-riley-sun',
    assignmentEntryId: 'scheduling-assignment-entry-triage-day-6',
    eventId: 'scheduling-shift-assignment-event-riley-sun-triage',
  },
];

export interface CalendarSchedulingEventSet {
  shifts: CalendarSchedulingEvent[];
  assignments: CalendarSchedulingEvent[];
}

export function projectCalendarSchedulingEvents(days: string[]): CalendarSchedulingEventSet {
  const assignmentEntries = projectAssignmentEntries(days);

  return {
    shifts: calendarShiftEntries.flatMap((event) => projectShiftEvent(event, days)),
    assignments: assignmentEntries.flatMap((event) => projectAssignmentEvent(event, days)),
  };
}

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

  const userIds = event.metadata.assignedUserIds ?? [];

  return userIds.flatMap((userId) => {
    const user = calendarSchedulingUsers.find((candidate) => candidate.id === userId);
    return user ? [user] : [];
  });
}

function projectShiftEvent(event: CalendarShiftEntry, days: string[]): CalendarSchedulingEvent[] {
  const date = days[event.dayIndex];

  if (!date) {
    return [];
  }

  return [
    {
      id: event.id,
      type: 'scheduling.shift',
      sourceModule: 'calendar-scheduling',
      title: event.title,
      start: `${date}T${event.startTime}`,
      end: `${date}T${event.endTime}`,
      resourceIds: [event.userId],
      metadata: {
        dayIndex: event.dayIndex,
        shiftEntryId: event.id,
        userId: event.userId,
      },
    },
  ];
}

function projectAssignmentEntries(days: string[]): CalendarAssignmentEntry[] {
  return calendarAssignments.flatMap((assignment) =>
    days.map((_day, dayIndex) => ({
      id: createAssignmentEntryId(assignment.id, dayIndex),
      assignmentId: assignment.id,
      dayIndex,
      title: assignment.title,
      startTime: '09:00:00',
      endTime: '17:00:00',
      status: 'active',
      capacity: assignment.capacity,
      colorId: assignment.colorId,
    })),
  );
}

function projectAssignmentEvent(event: CalendarAssignmentEntry, days: string[]): CalendarSchedulingEvent[] {
  const date = days[event.dayIndex];

  if (!date) {
    return [];
  }

  const assignedShiftIds = calendarShiftAssignments
    .filter((relationship) => relationship.assignmentEntryId === event.id)
    .map((relationship) => relationship.shiftEntryId);
  const assignedUserIds = assignedShiftIds.flatMap((shiftId) => {
    const shift = calendarShiftEntries.find((entry) => entry.id === shiftId);
    return shift ? [shift.userId] : [];
  });

  return [
    {
      id: event.id,
      type: 'scheduling.assignment',
      sourceModule: 'calendar-assignment',
      title: event.title,
      start: `${date}T${event.startTime}`,
      end: `${date}T${event.endTime}`,
      color: calendarMatrixColorMap[event.colorId],
      resourceIds: [event.assignmentId],
      metadata: {
        dayIndex: event.dayIndex,
        assignmentId: event.assignmentId,
        assignmentEntryId: event.id,
        capacity: event.capacity,
        assignedCount: assignedShiftIds.length,
        assignedShiftIds,
        assignedUserIds,
      },
    },
  ];
}

function createAssignmentEntryId(assignmentId: string, dayIndex: number) {
  const token = assignmentId.replace('assignment-', '');
  return `assignment-entry-${token}-day-${dayIndex}`;
}
