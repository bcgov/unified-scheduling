import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { calendarMatrixTestColorMap, type CalendarMatrixTestColorId } from './calendarMatrixTestColors';

export interface CalendarMatrixTestBaseItem {
  id: string;
  type: string;
  title: string;
  subtitle?: string;
  meta?: CalendarMatrixMetaItem[];
  avatarText?: string;
}

export interface CalendarMatrixTestUser extends CalendarMatrixTestBaseItem {
  type: 'user';
}

export interface CalendarMatrixTestAssignment extends CalendarMatrixTestBaseItem {
  type: 'assignment';
  assignmentCode: string;
  capacity: number;
  colorId: CalendarMatrixTestColorId;
}

type CalendarMatrixTestEventStatus = 'active' | 'draft';

export interface CalendarMatrixTestShiftEntry {
  id: string;
  userId: string;
  dayIndex: number;
  title: string;
  startTime: string;
  endTime: string;
  status: CalendarMatrixTestEventStatus;
}

export interface CalendarMatrixTestAssignmentEntry {
  id: string;
  assignmentId: string;
  dayIndex: number;
  title: string;
  startTime: string;
  endTime: string;
  status: CalendarMatrixTestEventStatus;
  capacity: number;
  colorId: CalendarMatrixTestColorId;
}

export interface CalendarMatrixTestShiftAssignmentEntry {
  id: string;
  shiftEntryId: string;
  assignmentEntryId: string;
  eventId: string;
}

export interface CalendarMatrixTestEventMetadata {
  dayIndex: number;
  shiftEntryId?: string;
  userId?: string;
  assignmentId?: string;
  assignmentEntryId?: string;
  capacity?: number;
  assignedCount?: number;
  assignedShiftIds?: string[];
  assignedUserIds?: string[];
}

export interface CalendarMatrixTestCalendarEvent extends CalendarEventBase {
  matrixTest: CalendarMatrixTestEventMetadata;
}

export const calendarMatrixTestUsers: CalendarMatrixTestUser[] = [
  {
    id: 'matrix-test-user-avery',
    type: 'user',
    title: 'Avery Chen',
    subtitle: 'Senior coordinator',
    meta: [{ label: 'Availability', value: 'Full time' }],
    avatarText: 'AC',
  },
  {
    id: 'matrix-test-user-jordan',
    type: 'user',
    title: 'Jordan Patel',
    subtitle: 'Program lead',
    meta: [{ label: 'Availability', value: 'Full time' }],
    avatarText: 'JP',
  },
  {
    id: 'matrix-test-user-morgan',
    type: 'user',
    title: 'Morgan Lee',
    subtitle: 'Floater',
    meta: [{ label: 'Availability', value: 'Part time' }],
    avatarText: 'ML',
  },
  {
    id: 'matrix-test-user-riley',
    type: 'user',
    title: 'Riley Smith',
    subtitle: 'Coverage support',
    meta: [{ label: 'Availability', value: 'On call' }],
    avatarText: 'RS',
  },
];

export const calendarMatrixTestDays = [
  { dayIndex: 0 },
  { dayIndex: 1 },
  { dayIndex: 2 },
  { dayIndex: 3 },
  { dayIndex: 4 },
  { dayIndex: 5 },
  { dayIndex: 6 },
] as const;

export const calendarMatrixTestAssignments: CalendarMatrixTestAssignment[] = [
  {
    id: 'matrix-test-assignment-intake',
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
    id: 'matrix-test-assignment-outreach',
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
    id: 'matrix-test-assignment-triage',
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
    id: 'matrix-test-assignment-admin',
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

export const calendarMatrixTestShiftEntries: CalendarMatrixTestShiftEntry[] = [
  {
    id: 'matrix-test-shift-avery-mon',
    userId: 'matrix-test-user-avery',
    dayIndex: 0,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'matrix-test-shift-avery-tue',
    userId: 'matrix-test-user-avery',
    dayIndex: 1,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-avery-wed',
    userId: 'matrix-test-user-avery',
    dayIndex: 2,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-avery-fri',
    userId: 'matrix-test-user-avery',
    dayIndex: 4,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-jordan-mon',
    userId: 'matrix-test-user-jordan',
    dayIndex: 0,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-jordan-tue',
    userId: 'matrix-test-user-jordan',
    dayIndex: 1,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'matrix-test-shift-jordan-thu',
    userId: 'matrix-test-user-jordan',
    dayIndex: 3,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-jordan-fri',
    userId: 'matrix-test-user-jordan',
    dayIndex: 4,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-morgan-tue',
    userId: 'matrix-test-user-morgan',
    dayIndex: 1,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-morgan-wed',
    userId: 'matrix-test-user-morgan',
    dayIndex: 2,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'matrix-test-shift-morgan-thu',
    userId: 'matrix-test-user-morgan',
    dayIndex: 3,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-morgan-sat',
    userId: 'matrix-test-user-morgan',
    dayIndex: 5,
    title: 'Weekend shift',
    startTime: '10:00:00',
    endTime: '14:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-riley-mon',
    userId: 'matrix-test-user-riley',
    dayIndex: 0,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-riley-wed',
    userId: 'matrix-test-user-riley',
    dayIndex: 2,
    title: 'Scheduled shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'active',
  },
  {
    id: 'matrix-test-shift-riley-thu',
    userId: 'matrix-test-user-riley',
    dayIndex: 3,
    title: 'Draft shift',
    startTime: '09:00:00',
    endTime: '17:00:00',
    status: 'draft',
  },
  {
    id: 'matrix-test-shift-riley-sun',
    userId: 'matrix-test-user-riley',
    dayIndex: 6,
    title: 'Weekend shift',
    startTime: '11:00:00',
    endTime: '15:00:00',
    status: 'active',
  },
];

export const calendarMatrixTestShiftAssignments: CalendarMatrixTestShiftAssignmentEntry[] = [
  {
    id: 'matrix-test-shift-assignment-avery-mon-intake',
    shiftEntryId: 'matrix-test-shift-avery-mon',
    assignmentEntryId: 'matrix-test-assignment-entry-intake-day-0',
    eventId: 'matrix-test-shift-assignment-event-avery-mon-intake',
  },
  {
    id: 'matrix-test-shift-assignment-avery-mon-outreach',
    shiftEntryId: 'matrix-test-shift-avery-mon',
    assignmentEntryId: 'matrix-test-assignment-entry-outreach-day-0',
    eventId: 'matrix-test-shift-assignment-event-avery-mon-outreach',
  },
  {
    id: 'matrix-test-shift-assignment-jordan-mon-triage',
    shiftEntryId: 'matrix-test-shift-jordan-mon',
    assignmentEntryId: 'matrix-test-assignment-entry-triage-day-0',
    eventId: 'matrix-test-shift-assignment-event-jordan-mon-triage',
  },
  {
    id: 'matrix-test-shift-assignment-jordan-mon-intake',
    shiftEntryId: 'matrix-test-shift-jordan-mon',
    assignmentEntryId: 'matrix-test-assignment-entry-intake-day-0',
    eventId: 'matrix-test-shift-assignment-event-jordan-mon-intake',
  },
  {
    id: 'matrix-test-shift-assignment-riley-mon-intake',
    shiftEntryId: 'matrix-test-shift-riley-mon',
    assignmentEntryId: 'matrix-test-assignment-entry-intake-day-0',
    eventId: 'matrix-test-shift-assignment-event-riley-mon-intake',
  },
  {
    id: 'matrix-test-shift-assignment-avery-tue-outreach',
    shiftEntryId: 'matrix-test-shift-avery-tue',
    assignmentEntryId: 'matrix-test-assignment-entry-outreach-day-1',
    eventId: 'matrix-test-shift-assignment-event-avery-tue-outreach',
  },
  {
    id: 'matrix-test-shift-assignment-jordan-tue-admin',
    shiftEntryId: 'matrix-test-shift-jordan-tue',
    assignmentEntryId: 'matrix-test-assignment-entry-admin-day-1',
    eventId: 'matrix-test-shift-assignment-event-jordan-tue-admin',
  },
  {
    id: 'matrix-test-shift-assignment-jordan-tue-outreach',
    shiftEntryId: 'matrix-test-shift-jordan-tue',
    assignmentEntryId: 'matrix-test-assignment-entry-outreach-day-1',
    eventId: 'matrix-test-shift-assignment-event-jordan-tue-outreach',
  },
  {
    id: 'matrix-test-shift-assignment-morgan-tue-outreach',
    shiftEntryId: 'matrix-test-shift-morgan-tue',
    assignmentEntryId: 'matrix-test-assignment-entry-outreach-day-1',
    eventId: 'matrix-test-shift-assignment-event-morgan-tue-outreach',
  },
  {
    id: 'matrix-test-shift-assignment-avery-wed-intake',
    shiftEntryId: 'matrix-test-shift-avery-wed',
    assignmentEntryId: 'matrix-test-assignment-entry-intake-day-2',
    eventId: 'matrix-test-shift-assignment-event-avery-wed-intake',
  },
  {
    id: 'matrix-test-shift-assignment-morgan-wed-intake',
    shiftEntryId: 'matrix-test-shift-morgan-wed',
    assignmentEntryId: 'matrix-test-assignment-entry-intake-day-2',
    eventId: 'matrix-test-shift-assignment-event-morgan-wed-intake',
  },
  {
    id: 'matrix-test-shift-assignment-riley-wed-triage',
    shiftEntryId: 'matrix-test-shift-riley-wed',
    assignmentEntryId: 'matrix-test-assignment-entry-triage-day-2',
    eventId: 'matrix-test-shift-assignment-event-riley-wed-triage',
  },
  {
    id: 'matrix-test-shift-assignment-jordan-thu-admin',
    shiftEntryId: 'matrix-test-shift-jordan-thu',
    assignmentEntryId: 'matrix-test-assignment-entry-admin-day-3',
    eventId: 'matrix-test-shift-assignment-event-jordan-thu-admin',
  },
  {
    id: 'matrix-test-shift-assignment-morgan-thu-triage',
    shiftEntryId: 'matrix-test-shift-morgan-thu',
    assignmentEntryId: 'matrix-test-assignment-entry-triage-day-3',
    eventId: 'matrix-test-shift-assignment-event-morgan-thu-triage',
  },
  {
    id: 'matrix-test-shift-assignment-riley-thu-triage',
    shiftEntryId: 'matrix-test-shift-riley-thu',
    assignmentEntryId: 'matrix-test-assignment-entry-triage-day-3',
    eventId: 'matrix-test-shift-assignment-event-riley-thu-triage',
  },
  {
    id: 'matrix-test-shift-assignment-riley-thu-admin',
    shiftEntryId: 'matrix-test-shift-riley-thu',
    assignmentEntryId: 'matrix-test-assignment-entry-admin-day-3',
    eventId: 'matrix-test-shift-assignment-event-riley-thu-admin',
  },
  {
    id: 'matrix-test-shift-assignment-avery-fri-triage',
    shiftEntryId: 'matrix-test-shift-avery-fri',
    assignmentEntryId: 'matrix-test-assignment-entry-triage-day-4',
    eventId: 'matrix-test-shift-assignment-event-avery-fri-triage',
  },
  {
    id: 'matrix-test-shift-assignment-jordan-fri-outreach',
    shiftEntryId: 'matrix-test-shift-jordan-fri',
    assignmentEntryId: 'matrix-test-assignment-entry-outreach-day-4',
    eventId: 'matrix-test-shift-assignment-event-jordan-fri-outreach',
  },
  {
    id: 'matrix-test-shift-assignment-morgan-sat-intake',
    shiftEntryId: 'matrix-test-shift-morgan-sat',
    assignmentEntryId: 'matrix-test-assignment-entry-intake-day-5',
    eventId: 'matrix-test-shift-assignment-event-morgan-sat-intake',
  },
  {
    id: 'matrix-test-shift-assignment-riley-sun-triage',
    shiftEntryId: 'matrix-test-shift-riley-sun',
    assignmentEntryId: 'matrix-test-assignment-entry-triage-day-6',
    eventId: 'matrix-test-shift-assignment-event-riley-sun-triage',
  },
];

export interface CalendarMatrixTestEventSet {
  shifts: CalendarMatrixTestCalendarEvent[];
  assignments: CalendarMatrixTestCalendarEvent[];
}

export function projectCalendarMatrixTestEvents(days: string[]): CalendarMatrixTestEventSet {
  const assignmentEntries = projectAssignmentEntries(days);

  return {
    shifts: calendarMatrixTestShiftEntries.flatMap((event) => projectShiftEvent(event, days)),
    assignments: assignmentEntries.flatMap((event) => projectAssignmentEvent(event, days)),
  };
}

export function isCalendarMatrixTestCalendarEvent(event: CalendarEventBase): event is CalendarMatrixTestCalendarEvent {
  return 'matrixTest' in event && typeof event.matrixTest === 'object' && event.matrixTest !== null;
}

export function getCalendarMatrixTestAssignmentCapacity(event: CalendarEventBase) {
  if (!isCalendarMatrixTestCalendarEvent(event)) {
    return undefined;
  }

  const capacity = event.matrixTest.capacity ?? 0;
  const assignedCount = event.matrixTest.assignedCount ?? 0;

  return {
    capacity,
    assignedCount,
    filledCount: Math.min(assignedCount, capacity),
    overflowCount: Math.max(assignedCount - capacity, 0),
  };
}

export function getCalendarMatrixTestAssignedUsers(event: CalendarEventBase) {
  if (!isCalendarMatrixTestCalendarEvent(event)) {
    return [];
  }

  const userIds = event.matrixTest.assignedUserIds ?? [];

  return userIds.flatMap((userId) => {
    const user = calendarMatrixTestUsers.find((candidate) => candidate.id === userId);
    return user ? [user] : [];
  });
}

function projectShiftEvent(event: CalendarMatrixTestShiftEntry, days: string[]): CalendarMatrixTestCalendarEvent[] {
  const date = days[event.dayIndex];

  if (!date) {
    return [];
  }

  return [
    {
      id: event.id,
      type: 'scheduling.shift',
      sourceModule: 'calendar-matrix-test',
      title: event.title,
      start: `${date}T${event.startTime}`,
      end: `${date}T${event.endTime}`,
      status: event.status,
      resourceIds: [event.userId],
      matrixTest: {
        dayIndex: event.dayIndex,
        shiftEntryId: event.id,
        userId: event.userId,
      },
    },
  ];
}

function projectAssignmentEntries(days: string[]): CalendarMatrixTestAssignmentEntry[] {
  return calendarMatrixTestAssignments.flatMap((assignment) =>
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

function projectAssignmentEvent(
  event: CalendarMatrixTestAssignmentEntry,
  days: string[],
): CalendarMatrixTestCalendarEvent[] {
  const date = days[event.dayIndex];

  if (!date) {
    return [];
  }

  const assignedShiftIds = calendarMatrixTestShiftAssignments
    .filter((relationship) => relationship.assignmentEntryId === event.id)
    .map((relationship) => relationship.shiftEntryId);
  const assignedUserIds = assignedShiftIds.flatMap((shiftId) => {
    const shift = calendarMatrixTestShiftEntries.find((entry) => entry.id === shiftId);
    return shift ? [shift.userId] : [];
  });

  return [
    {
      id: event.id,
      type: 'scheduling.assignment',
      sourceModule: 'calendar-matrix-test',
      title: event.title,
      start: `${date}T${event.startTime}`,
      end: `${date}T${event.endTime}`,
      status: event.status,
      color: calendarMatrixTestColorMap[event.colorId],
      resourceIds: [event.assignmentId],
      matrixTest: {
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
  const token = assignmentId.replace('matrix-test-assignment-', '');
  return `matrix-test-assignment-entry-${token}-day-${dayIndex}`;
}
