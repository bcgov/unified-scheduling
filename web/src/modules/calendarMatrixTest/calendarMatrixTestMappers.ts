import {
  addDays,
  DATE_FORMAT,
  formatCalendarEventTimeRange,
  formatLocalDateOnly,
  hasExplicitTimeZoneOffset,
  parseLocalDateOnly,
  startOfWeek,
  toCalendarDateOnly,
  toDateTime,
} from '@/utils/date';
import type { CalendarPeriod } from '@/modules/calendar/calendarStore';
import type { CalendarEventBase, CalendarQueryContext } from '@/modules/calendar/calendarTypes';
import {
  CalendarMatrixActionType,
  type CalendarMatrixCell,
  type CalendarMatrixCellHeader,
  type CalendarMatrixDay,
  type CalendarMatrixActionDisplay,
  type CalendarMatrixEventItem,
  type CalendarMatrixResource,
  type CalendarMatrixSidePanelItem,
  type CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import {
  calendarMatrixTestDays,
  calendarMatrixTestAssignments,
  calendarMatrixTestShiftAssignments,
  calendarMatrixTestUsers,
  isCalendarMatrixTestCalendarEvent,
  projectCalendarMatrixTestEvents,
} from './calendarMatrixTestData';
import { calendarMatrixTestActionIds } from './calendarMatrixTestActionIds';
import { mdiAlertCircle } from '@mdi/js';

const defaultCalendarMatrixTestTimeZone = 'America/Vancouver';

export function buildCalendarMatrixScheduleTestViewModel(
  context: CalendarQueryContext,
  period: CalendarPeriod,
): CalendarMatrixViewModel {
  if (period === 'month') {
    return {
      unsupportedMessage: 'Not supported',
      days: [],
      primaryColumn: {
        label: 'TEAM',
        resources: [],
      },
      cells: [],
    };
  }

  const days = buildDays(context.startDate, period);
  const timeZone = resolveMatrixTimeZone(context);
  const dateValues = days.map((day) => day.date);
  const eventSet = projectCalendarMatrixTestEvents(dateValues);
  const resources = buildUserResourceRows();
  const cells: CalendarMatrixCell[] = [];

  for (const user of resources) {
    for (const day of days) {
      const shiftEvents = eventSet.shifts.filter(
        (event) => event.resourceIds?.includes(user.id) && isEventOnMatrixDate(event, day.date, timeZone),
      );
      const assignmentEntryIds = assignmentEntryIdsForShiftEvents(shiftEvents);
      const assignmentEvents = eventSet.assignments.filter((event) => {
        const assignmentEntryId = resolveAssignmentEntryId(event);
        return assignmentEntryId ? assignmentEntryIds.has(assignmentEntryId) : false;
      });

      cells.push({
        resourceId: user.id,
        date: day.date,
        headers: shiftEvents.map((event) => buildCellHeader(event, timeZone)),
        groups: [
          {
            id: 'assignments',
            variant: 'primary',
            showColorBar: true,
            action: buildAddAction(),
            events: toMatrixEventItems(assignmentEvents),
          },
        ],
      });
    }
  }

  return {
    days,
    timeZone,
    primaryColumn: {
      label: 'TEAM',
      resources,
    },
    cells,
    sidePanel: {
      label: 'ASSIGNMENTS',
      actionId: calendarMatrixTestActionIds.addAssignment,
      actionLabel: 'Add assignment',
      items: buildAssignmentSidePanelItems(),
    },
  };
}

export function buildCalendarMatrixAssignmentTestViewModel(
  context: CalendarQueryContext,
  period: CalendarPeriod,
): CalendarMatrixViewModel {
  if (period === 'month') {
    return {
      unsupportedMessage: 'Not supported',
      days: [],
      primaryColumn: {
        label: 'ASSIGNMENTS',
        resources: [],
      },
      cells: [],
    };
  }

  const days = buildDays(context.startDate, period);
  const timeZone = resolveMatrixTimeZone(context);
  const dateValues = days.map((day) => day.date);
  const eventSet = projectCalendarMatrixTestEvents(dateValues);
  const resources = buildAssignmentResourceRows();
  const cells: CalendarMatrixCell[] = [];

  for (const assignment of resources) {
    for (const day of days) {
      const assignmentEvents = eventSet.assignments.filter(
        (event) =>
          isCalendarMatrixTestCalendarEvent(event) &&
          event.matrixTest.assignmentId === assignment.id &&
          isEventOnMatrixDate(event, day.date, timeZone),
      );

      cells.push({
        resourceId: assignment.id,
        date: day.date,
        headers: [],
        groups: [
          {
            id: 'assignments',
            variant: 'primary',
            showColorBar: true,
            action: buildAddAction(),
            events: toMatrixEventItems(assignmentEvents),
          },
        ],
      });
    }
  }

  return {
    days,
    timeZone,
    primaryColumn: {
      label: 'ASSIGNMENTS',
      resources,
    },
    cells,
    sidePanel: {
      label: 'TEAM',
      actionId: calendarMatrixTestActionIds.addResource,
      actionLabel: 'Schedule staff',
      items: buildUserSidePanelItems(),
    },
  };
}

function buildUserResourceRows(): CalendarMatrixResource[] {
  return calendarMatrixTestUsers.map((user) => ({
    id: user.id,
    type: user.type,
    title: user.title,
    subtitle: user.subtitle,
    meta: user.meta,
    avatarText: user.avatarText,
    action: {
      actionId: calendarMatrixTestActionIds.addResource,
      label: '+',
      ariaLabel: `Add resource action for ${user.title}`,
    },
  }));
}

function buildAssignmentResourceRows(): CalendarMatrixResource[] {
  return calendarMatrixTestAssignments.map((assignment) => ({
    id: assignment.id,
    type: assignment.type,
    title: assignment.title,
    subtitle: assignment.subtitle,
    meta: [...(assignment.meta ?? []), { label: '', value: '9:00 AM - 5:00 PM' }],
    avatarText: assignment.avatarText,
    action: {
      actionId: calendarMatrixTestActionIds.addResource,
      label: '+',
      ariaLabel: `Add resource action for ${assignment.title}`,
    },
  }));
}

function buildAssignmentSidePanelItems(): CalendarMatrixSidePanelItem[] {
  return calendarMatrixTestAssignments.map((assignment) => ({
    id: assignment.id,
    type: assignment.type,
    title: assignment.title,
    subtitle: assignment.subtitle,
    meta: assignment.meta,
    avatarText: assignment.avatarText,
    draggable: true,
    payload: {
      assignmentCode: assignment.assignmentCode,
      capacity: assignment.capacity,
    },
  }));
}

function buildUserSidePanelItems(): CalendarMatrixSidePanelItem[] {
  return calendarMatrixTestUsers.map((user) => ({
    id: user.id,
    type: user.type,
    title: user.title,
    subtitle: user.subtitle,
    meta: user.meta,
    avatarText: user.avatarText,
    draggable: true,
    payload: {
      userId: user.id,
    },
  }));
}

function assignmentEntryIdsForShiftEvents(events: ReadonlyArray<CalendarEventBase>) {
  const shiftEntryIds = new Set(events.map(resolveShiftEntryId).filter((id): id is string => Boolean(id)));

  return new Set(
    calendarMatrixTestShiftAssignments
      .filter((relationship) => shiftEntryIds.has(relationship.shiftEntryId))
      .map((relationship) => relationship.assignmentEntryId),
  );
}

function resolveShiftEntryId(event: CalendarEventBase) {
  return isCalendarMatrixTestCalendarEvent(event) ? event.matrixTest.shiftEntryId : undefined;
}

function resolveAssignmentEntryId(event: CalendarEventBase) {
  return isCalendarMatrixTestCalendarEvent(event) ? event.matrixTest.assignmentEntryId : undefined;
}

function buildCellHeader(
  event: CalendarEventBase,
  timeZone = defaultCalendarMatrixTestTimeZone,
): CalendarMatrixCellHeader {
  return {
    id: event.id,
    text: formatCalendarEventTimeRange(event.start, event.end, {
      allDay: event.allDay,
      timeZone: event.timeZoneId ?? timeZone,
    }),
    title: event.title,
    status: event.statusTypeCode,
    color: event.color,
    actionId: calendarMatrixTestActionIds.viewHeaderDetails,
    action: buildPulldownAction(),
    payload: event,
  };
}

export function getCalendarEventDateKey(
  value: string | Date | undefined | null,
  timeZone?: string,
  locale = 'en-CA',
): string | undefined {
  if (!value) {
    return undefined;
  }

  if (typeof value === 'string') {
    const dateOnly = toCalendarDateOnly(value);

    if (!dateOnly || !/^\d{4}-\d{2}-\d{2}$/.test(dateOnly)) {
      return undefined;
    }

    if (!timeZone || !hasExplicitTimeZoneOffset(value)) {
      return dateOnly;
    }
  }

  const dateTime = toDateTime(value, timeZone);

  if (!dateTime.isValid) {
    return undefined;
  }

  return dateTime.setLocale(locale).toFormat(DATE_FORMAT);
}

function isEventOnMatrixDate(event: CalendarEventBase, date: string, timeZone: string) {
  return getCalendarEventDateKey(event.start, event.timeZoneId ?? timeZone) === date;
}

function resolveMatrixTimeZone(context: CalendarQueryContext) {
  const timeZone = context.filters.timeZoneId ?? context.filters.timeZone;
  return typeof timeZone === 'string' && timeZone.trim() ? timeZone : defaultCalendarMatrixTestTimeZone;
}

function buildAddAction(): CalendarMatrixActionDisplay {
  return {
    actionId: calendarMatrixTestActionIds.addOnEvent,
    text: '+',
    ariaLabel: 'Add an event',
    type: CalendarMatrixActionType.Button,
  };
}

function buildPulldownAction(): CalendarMatrixActionDisplay {
  return {
    actionId: calendarMatrixTestActionIds.showConflict,
    icon: mdiAlertCircle,
    ariaLabel: 'Show conflict details',
    type: CalendarMatrixActionType.Button,
  };
}

function buildDays(startDate: string, period: CalendarPeriod): CalendarMatrixDay[] {
  const firstDate = period === 'day' ? startDate : startOfWeek(startDate);
  const dayCount = resolveDayCount(period);
  const today = formatLocalDateOnly(new Date());

  return calendarMatrixTestDays.slice(0, dayCount).map(({ dayIndex }) => {
    const date = addDays(firstDate, dayIndex);

    return {
      date,
      label: formatDayLabel(date),
      isToday: date === today,
    };
  });
}

function resolveDayCount(period: CalendarPeriod) {
  switch (period) {
    case 'day':
      return 1;
    case 'week':
      return 7;
    case 'work-week':
      return 5;
    case 'month':
      return 0;
  }
}

function formatDayLabel(value: string) {
  return new Intl.DateTimeFormat('en-CA', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  }).format(parseLocalDateOnly(value));
}

function toMatrixEventItems(events: ReadonlyArray<CalendarEventBase>): CalendarMatrixEventItem[] {
  return events.map((event) => ({
    event,
    display: {
      color: event.color,
      status: event.statusTypeCode,
      draggable: false,
    },
  }));
}
