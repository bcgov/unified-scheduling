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
import { selectCalendarConflicts, selectContribution } from '@/modules/calendar/calendarSelectors';
import type {
  CalendarConflict,
  CalendarDataResponse,
  CalendarEventBase,
  CalendarQueryContext,
} from '@/modules/calendar/calendarTypes';
import {
  CalendarMatrixActionType,
  type CalendarMatrixActionDisplay,
  type CalendarMatrixCell,
  type CalendarMatrixCellHeader,
  type CalendarMatrixDay,
  type CalendarMatrixEventItem,
  type CalendarMatrixResource,
  type CalendarMatrixSidePanelItem,
  type CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import {
  calendarSchedulingDays,
  isCalendarSchedulingEvent,
  type CalendarAssignmentCapacitySlotState,
  type CalendarAssignmentPartialCoverageShift,
  type CalendarSchedulingEvent,
} from './calendarSchedulingData';
import type { CalendarSchedulingUserResource } from './contributions/calendarSchedulingEventsContribution';
import {
  schedulingAssignmentContributionId,
  type CalendarSchedulingAssignmentResource,
} from './contributions/calendarSchedulingAssignmentsContribution';
import { calendarSchedulingActionIds } from './calendarSchedulingActionIds';
import { calendarMatrixColorMap } from './calendarSchedulingColors';
import { mdiAlertCircle, mdiCalendarSync } from '@mdi/js';

const defaultCalendarSchedulingTimeZone = 'America/Vancouver';
const schedulingShiftContributionId = 'scheduling.shift-events';
const unassignedScheduleResourceId = 'scheduling.unassigned';

interface AssignmentMatrixResource extends CalendarMatrixResource {
  assignmentDefinitionId?: number;
  locationId?: number;
  assignmentCategoryTypeId?: number;
  assignmentCategoryTypeCode?: string;
  assignmentSubCategoryTypeId?: number;
  assignmentSubCategoryTypeCode?: string;
}

export function buildCalendarSchedulingViewModel(
  response: CalendarDataResponse,
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
  const schedulingEvents = selectSchedulingShiftEvents(response);
  const shiftEvents = schedulingEvents.filter(isShiftEvent);
  const assignmentResources = selectSchedulingAssignmentResources(response);
  const conflicts = selectCalendarConflicts(response);
  const assignmentEvents = selectSchedulingAssignmentEvents(response)
    .map((event) => withResolvedAssignmentDefinitionId(event, assignmentResources))
    .map((event) => withCalendarConflicts(event, conflicts));
  const resources = buildUserResourceRows(response);
  const scheduleResources = hasUnassignedScheduleEvents(shiftEvents, assignmentEvents, days, timeZone)
    ? [...resources, buildUnassignedResourceRow()]
    : resources;
  const cells: CalendarMatrixCell[] = [];

  for (const user of scheduleResources) {
    for (const day of days) {
      const isUnassignedRow = user.id === unassignedScheduleResourceId;
      const userShiftEvents = isUnassignedRow
        ? shiftEvents.filter((event) => isUnassignedShiftEvent(event) && isEventOnMatrixDate(event, day.date, timeZone))
        : shiftEvents.filter(
            (event) => event.resourceIds?.includes(user.id) && isEventOnMatrixDate(event, day.date, timeZone),
          );
      const userAssignmentEvents = isUnassignedRow
        ? assignmentEvents.filter(
            (event) => isUnlinkedAssignmentEvent(event) && isEventOnMatrixDate(event, day.date, timeZone),
          )
        : assignmentEvents.filter(
            (event) =>
              isEventOnMatrixDate(event, day.date, timeZone) &&
              assignmentEventBelongsToUserScheduleCell(event, user.id, userShiftEvents),
          );

      cells.push({
        resourceId: user.id,
        date: day.date,
        headers: userShiftEvents.map((event) => buildCellHeader(event, timeZone)),
        groups: [
          {
            id: 'assignments',
            variant: 'primary',
            showColorBar: true,
            events: toScheduleMatrixEventItems(userAssignmentEvents, userShiftEvents),
          },
        ],
      });
    }
  }

  return {
    days,
    timeZone,
    payload: {
      assignmentEvents,
    },
    primaryColumn: {
      label: 'TEAM',
      resources: scheduleResources,
    },
    cells,
    sidePanel: {
      label: 'ASSIGNMENTS',
      actionId: calendarSchedulingActionIds.addAssignment,
      actionLabel: 'Add Assignment',
      items: buildAssignmentSidePanelItems(response),
    },
  };
}

function buildUnassignedResourceRow(): CalendarMatrixResource {
  return {
    id: unassignedScheduleResourceId,
    type: 'unassigned',
    title: 'Unassigned',
    subtitle: 'unassigned shifts and/or assignments',
    avatarText: '?',
  };
}

function hasUnassignedScheduleEvents(
  shiftEvents: ReadonlyArray<CalendarEventBase>,
  assignmentEvents: ReadonlyArray<CalendarEventBase>,
  days: ReadonlyArray<CalendarMatrixDay>,
  timeZone: string,
) {
  return days.some((day) => {
    const hasUnassignedShift = shiftEvents.some(
      (event) => isUnassignedShiftEvent(event) && isEventOnMatrixDate(event, day.date, timeZone),
    );

    if (hasUnassignedShift) {
      return true;
    }

    return assignmentEvents.some(
      (event) => isUnlinkedAssignmentEvent(event) && isEventOnMatrixDate(event, day.date, timeZone),
    );
  });
}

export function buildCalendarAssignmentViewModel(
  response: CalendarDataResponse,
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
  const resources = buildAssignmentResourceRows(response);
  const conflicts = selectCalendarConflicts(response);
  const assignmentEvents = selectSchedulingAssignmentEvents(response).map((event) =>
    withCalendarConflicts(event, conflicts),
  );
  const shiftEvents = selectSchedulingShiftEvents(response).filter(isShiftEvent);
  const cells: CalendarMatrixCell[] = [];

  for (const assignment of resources) {
    for (const day of days) {
      const dayAssignmentEvents = assignmentEvents.filter(
        (event) =>
          isCalendarSchedulingEvent(event) &&
          assignmentEventBelongsToAssignmentResource(event, assignment, resources) &&
          isEventOnMatrixDate(event, day.date, timeZone),
      );
      const dayShiftEvents = shiftEvents.filter(
        (event) => !isCancelledStatus(event.statusTypeCode) && isEventOnMatrixDate(event, day.date, timeZone),
      );

      cells.push({
        resourceId: assignment.id,
        date: day.date,
        headers: [],
        payload: {
          shiftEvents: dayShiftEvents,
        },
        groups: [
          {
            id: 'assignments',
            variant: 'primary',
            showColorBar: true,
            events: toScheduleMatrixEventItems(dayAssignmentEvents, dayShiftEvents),
          },
        ],
      });
    }
  }

  return {
    days,
    timeZone,
    payload: {
      assignmentEvents,
    },
    primaryColumn: {
      label: 'ASSIGNMENTS',
      resources,
    },
    cells,
    sidePanel: {
      label: 'TEAM',
      actionId: calendarSchedulingActionIds.scheduleStaff,
      actionLabel: 'Schedule staff',
      items: buildUserSidePanelItems(response),
    },
  };
}

function buildUserResourceRows(response: CalendarDataResponse): CalendarMatrixResource[] {
  return selectSchedulingUserResources(response).map((user) => ({
    id: user.id,
    type: user.type,
    title: user.title,
    subtitle: user.subtitle,
    meta: user.meta,
    avatarText: user.avatarText,
    action: {
      actionId: calendarSchedulingActionIds.addResource,
      label: '+',
      ariaLabel: `Add Resource Action For ${user.title}`,
    },
  }));
}

function selectSchedulingShiftEvents(response: CalendarDataResponse) {
  const contribution = selectContribution(response, schedulingShiftContributionId);

  if (!contribution) {
    return [];
  }

  return contribution.events.filter(isCalendarSchedulingEvent);
}

function selectSchedulingAssignmentEvents(response: CalendarDataResponse) {
  const assignmentContribution = selectContribution(response, schedulingAssignmentContributionId);
  const shiftContribution = selectContribution(response, schedulingShiftContributionId);
  const assignmentContributionEvents = assignmentContribution?.events.filter(isCalendarSchedulingEvent) ?? [];
  const seenEventKeys = new Set(assignmentContributionEvents.map(createSchedulingAssignmentEventKey));
  const shiftContributionAssignmentEvents = (shiftContribution?.events.filter(isCalendarSchedulingEvent) ?? [])
    .filter(isAssignmentEvent)
    .filter((event) => {
      const key = createSchedulingAssignmentEventKey(event);
      if (seenEventKeys.has(key)) {
        return false;
      }

      seenEventKeys.add(key);
      return true;
    });

  return [...assignmentContributionEvents, ...shiftContributionAssignmentEvents];
}

function isShiftEvent(event: CalendarEventBase) {
  return event.type === 'scheduling.shift' || event.eventTypeCode === 'shift';
}

function isAssignmentEvent(event: CalendarEventBase) {
  return event.type === 'scheduling.assignment' || event.eventTypeCode === 'assignment';
}

function createSchedulingAssignmentEventKey(event: CalendarEventBase) {
  if (isCalendarSchedulingEvent(event) && event.metadata.assignmentEntryId) {
    return `assignment-entry-${event.metadata.assignmentEntryId}`;
  }

  return event.id;
}

function assignmentEventBelongsToAssignmentResource(
  event: CalendarEventBase,
  resource: AssignmentMatrixResource,
  resources: ReadonlyArray<AssignmentMatrixResource>,
) {
  const resourceId = resource.id;

  if (event.resourceIds?.includes(resourceId)) {
    return true;
  }

  if (!isCalendarSchedulingEvent(event)) {
    return false;
  }

  if (event.metadata.assignmentId === resourceId) {
    return true;
  }

  const eventResourceId = resolveAssignmentEventResourceId(event);
  if (eventResourceId) {
    return eventResourceId === resourceId;
  }

  return resolveAssignmentEventResourceIdFromDefinitionFields(event, resources) === resourceId;
}

function resolveAssignmentEventResourceId(event: CalendarEventBase) {
  if (event.resourceIds?.length) {
    return event.resourceIds[0];
  }

  if (!isCalendarSchedulingEvent(event)) {
    return undefined;
  }

  if (event.metadata.assignmentId) {
    return event.metadata.assignmentId;
  }

  const parsed = Number(event.metadata.assignmentDefinitionId);
  return Number.isInteger(parsed) && parsed > 0 ? createAssignmentResourceId(parsed) : undefined;
}

function createAssignmentResourceId(assignmentDefinitionId: number) {
  return `assignment-definition-${assignmentDefinitionId}`;
}

function resolveAssignmentEventResourceIdFromDefinitionFields(
  event: CalendarEventBase,
  resources: ReadonlyArray<AssignmentMatrixResource>,
) {
  if (!isCalendarSchedulingEvent(event)) {
    return undefined;
  }

  const candidates = resources.filter((resource) => assignmentEventMatchesDefinitionFields(event, resource));
  return candidates.length === 1 ? candidates[0]?.id : undefined;
}

function assignmentEventMatchesDefinitionFields(event: CalendarSchedulingEvent, resource: AssignmentMatrixResource) {
  if (normalizeAssignmentText(event.title) !== normalizeAssignmentText(resource.title)) {
    return false;
  }

  if (!optionalValuesMatch(event.metadata.assignmentCategoryTypeId, resource.assignmentCategoryTypeId)) {
    return false;
  }

  if (!optionalValuesMatch(event.metadata.assignmentSubCategoryTypeId, resource.assignmentSubCategoryTypeId)) {
    return false;
  }

  if (!optionalTextValuesMatch(event.metadata.assignmentCategoryTypeCode, resource.assignmentCategoryTypeCode)) {
    return false;
  }

  return optionalTextValuesMatch(event.metadata.assignmentSubCategoryTypeCode, resource.assignmentSubCategoryTypeCode);
}

function withResolvedAssignmentDefinitionId(
  event: CalendarEventBase,
  resources: ReadonlyArray<CalendarSchedulingAssignmentResource>,
): CalendarEventBase {
  if (!isCalendarSchedulingEvent(event) || event.metadata.assignmentDefinitionId) {
    return event;
  }

  const matchingResources = resources.filter((resource) => assignmentEventMatchesDefinitionFields(event, resource));
  const matchedResource = matchingResources.length === 1 ? matchingResources[0] : undefined;

  if (!matchedResource?.assignmentDefinitionId) {
    return event;
  }

  const resolvedEvent: CalendarSchedulingEvent = {
    ...event,
    metadata: {
      ...event.metadata,
      assignmentDefinitionId: String(matchedResource.assignmentDefinitionId),
    },
  };

  return resolvedEvent;
}

function withCalendarConflicts(
  event: CalendarEventBase,
  conflicts: ReadonlyArray<CalendarConflict>,
): CalendarEventBase {
  if (!isCalendarSchedulingEvent(event) || !isAssignmentEvent(event) || !event.metadata.eventId) {
    return event;
  }

  const eventConflicts = conflicts.filter(
    (conflict) =>
      conflict.entry.eventId === event.metadata.eventId || conflict.overlaps.eventId === event.metadata.eventId,
  );
  if (eventConflicts.length === 0) {
    return event;
  }

  const eventWithConflicts: CalendarSchedulingEvent = {
    ...event,
    metadata: {
      ...event.metadata,
      conflicts: eventConflicts,
    },
  };

  return eventWithConflicts;
}

function normalizeAssignmentText(value?: string | null) {
  return value?.trim().toLocaleLowerCase() ?? '';
}

function optionalValuesMatch(left?: number, right?: number) {
  return left == null || right == null || left === right;
}

function optionalTextValuesMatch(left?: string, right?: string) {
  return !left || !right || normalizeAssignmentText(left) === normalizeAssignmentText(right);
}

function assignmentEventBelongsToUserScheduleCell(
  assignmentEvent: CalendarEventBase,
  userId: string,
  userShiftEvents: CalendarEventBase[],
) {
  if (!isCalendarSchedulingEvent(assignmentEvent)) {
    return false;
  }

  if (assignmentEvent.metadata.assignedUserIds?.length) {
    return assignmentEvent.metadata.assignedUserIds.includes(userId);
  }

  const linkedShiftEntryIds = new Set(assignmentEvent.metadata.assignedShiftIds ?? []);
  if (linkedShiftEntryIds.size === 0) {
    return false;
  }

  return userShiftEvents.some(
    (shiftEvent) =>
      isCalendarSchedulingEvent(shiftEvent) && linkedShiftEntryIds.has(String(shiftEvent.metadata.shiftEntryId)),
  );
}

function isUnassignedShiftEvent(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return !event.resourceIds?.length;
  }

  return !event.metadata.userIds?.length && !event.metadata.userId && !event.resourceIds?.length;
}

function isUnlinkedAssignmentEvent(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return false;
  }

  return (event.metadata.assignedShiftIds ?? []).length === 0;
}

function resolveLinkedShiftsForAssignment(assignmentEvent: CalendarEventBase, userShiftEvents: CalendarEventBase[]) {
  if (!isCalendarSchedulingEvent(assignmentEvent)) {
    return [];
  }

  const linkedShiftEntryIds = new Set(assignmentEvent.metadata.assignedShiftIds ?? []);
  if (linkedShiftEntryIds.size === 0) {
    return [];
  }

  return userShiftEvents.filter(
    (shiftEvent) =>
      isCalendarSchedulingEvent(shiftEvent) && linkedShiftEntryIds.has(String(shiftEvent.metadata.shiftEntryId)),
  );
}

function isCancelledStatus(status?: string) {
  return status?.trim().toLowerCase().includes('cancel') === true;
}

function selectSchedulingUserResources(response: CalendarDataResponse): CalendarSchedulingUserResource[] {
  const contribution = selectContribution(response, schedulingShiftContributionId);

  if (!contribution?.resources) {
    return [];
  }

  return contribution.resources.flatMap((resource) => (isCalendarSchedulingUserResource(resource) ? [resource] : []));
}

function selectSchedulingAssignmentResources(response: CalendarDataResponse): CalendarSchedulingAssignmentResource[] {
  const contribution = selectContribution(response, schedulingAssignmentContributionId);

  if (!contribution?.resources) {
    return [];
  }

  return contribution.resources.flatMap((resource) =>
    isCalendarSchedulingAssignmentResource(resource) ? [resource] : [],
  );
}

function isCalendarSchedulingUserResource(resource: {
  id: string;
  type: string;
}): resource is CalendarSchedulingUserResource {
  return 'title' in resource && typeof resource.title === 'string';
}

function isCalendarSchedulingAssignmentResource(resource: {
  id: string;
  type: string;
}): resource is CalendarSchedulingAssignmentResource {
  return resource.type === 'assignment' && 'title' in resource && typeof resource.title === 'string';
}

function buildAssignmentResourceRows(response: CalendarDataResponse): AssignmentMatrixResource[] {
  const resources = selectSchedulingAssignmentResources(response).map((assignment) =>
    buildAssignmentResourceRow({
      id: assignment.id,
      title: assignment.title,
      subtitle: assignment.subtitle,
      meta: assignment.meta,
      avatarText: assignment.avatarText,
      assignmentDefinitionId: assignment.assignmentDefinitionId,
      locationId: assignment.locationId,
      assignmentCategoryTypeId: assignment.assignmentCategoryTypeId,
      assignmentCategoryTypeCode: assignment.assignmentCategoryTypeCode,
      assignmentSubCategoryTypeId: assignment.assignmentSubCategoryTypeId,
      assignmentSubCategoryTypeCode: assignment.assignmentSubCategoryTypeCode,
    }),
  );
  const resourceIds = new Set(resources.map((resource) => resource.id));
  const fallbackResources = selectSchedulingAssignmentEvents(response).flatMap((event) => {
    const resourceId = resolveAssignmentEventResourceId(event);
    if (!resourceId || resourceIds.has(resourceId)) {
      return [];
    }

    resourceIds.add(resourceId);
    return [
      buildAssignmentResourceRow({
        id: resourceId,
        title: event.title || 'Assignment',
      }),
    ];
  });

  return [...resources, ...fallbackResources];
}

function buildAssignmentResourceRow(resource: {
  id: string;
  title: string;
  subtitle?: string;
  meta?: CalendarMatrixResource['meta'];
  avatarText?: string;
  assignmentDefinitionId?: number;
  locationId?: number;
  assignmentCategoryTypeId?: number;
  assignmentCategoryTypeCode?: string;
  assignmentSubCategoryTypeId?: number;
  assignmentSubCategoryTypeCode?: string;
}): AssignmentMatrixResource {
  return {
    id: resource.id,
    type: 'assignment',
    title: resource.title,
    subtitle: resource.subtitle,
    meta: resource.meta,
    avatarText: resource.avatarText,
    assignmentDefinitionId: resource.assignmentDefinitionId,
    locationId: resource.locationId,
    assignmentCategoryTypeId: resource.assignmentCategoryTypeId,
    assignmentCategoryTypeCode: resource.assignmentCategoryTypeCode,
    assignmentSubCategoryTypeId: resource.assignmentSubCategoryTypeId,
    assignmentSubCategoryTypeCode: resource.assignmentSubCategoryTypeCode,
    action: {
      actionId: calendarSchedulingActionIds.addAssignmentResource,
      label: '+',
      ariaLabel: `Add Resource Action For ${resource.title}`,
    },
  };
}

function buildAssignmentSidePanelItems(response: CalendarDataResponse): CalendarMatrixSidePanelItem[] {
  return selectSchedulingAssignmentResources(response).map((assignment) => ({
    id: assignment.id,
    type: assignment.type,
    title: assignment.title,
    subtitle: assignment.subtitle,
    meta: assignment.meta,
    avatarText: assignment.avatarText,
    draggable: true,
    payload: {
      title: assignment.title,
      description: assignment.description,
      subtitle: assignment.subtitle,
      assignmentDefinitionId: assignment.assignmentDefinitionId,
      locationId: assignment.locationId,
      defaultStartTime: assignment.defaultStartTime,
      defaultEndTime: assignment.defaultEndTime,
      capacity: assignment.capacity,
      entries: assignment.entries,
    },
  }));
}

function buildUserSidePanelItems(response: CalendarDataResponse): CalendarMatrixSidePanelItem[] {
  return selectSchedulingUserResources(response).map((user) => ({
    id: user.id,
    type: user.type,
    title: user.title,
    subtitle: user.subtitle,
    meta: user.meta,
    avatarText: user.avatarText,
    draggable: true,
    payload: {
      userId: user.id,
      title: user.title,
    },
  }));
}

function buildCellHeader(
  event: CalendarEventBase,
  timeZone = defaultCalendarSchedulingTimeZone,
): CalendarMatrixCellHeader {
  return {
    id: event.id,
    text: formatCalendarEventTimeRange(event.start, event.end, {
      allDay: event.allDay,
      timeZone: event.timeZoneId ?? timeZone,
    }),
    title: event.title,
    status: event.statusTypeCode,
    color: resolveCalendarSchedulingColor(event.color),
    info: eventBelongsToSeries(event)
      ? {
          icons: [
            {
              icon: mdiCalendarSync,
              ariaLabel: 'Part of a shift series',
              title: 'Part of a shift series',
            },
          ],
        }
      : undefined,
    actionId: calendarSchedulingActionIds.viewHeaderDetails,
    payload: event,
  };
}

function eventBelongsToSeries(event: CalendarEventBase) {
  if (event.eventSeriesId != null) {
    return true;
  }

  return isCalendarSchedulingEvent(event) && event.metadata.shiftSeriesId != null;
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
  return typeof timeZone === 'string' && timeZone.trim() ? timeZone : defaultCalendarSchedulingTimeZone;
}

function buildDays(startDate: string, period: CalendarPeriod): CalendarMatrixDay[] {
  const firstDate = period === 'day' ? startDate : startOfWeek(startDate);
  const dayCount = resolveDayCount(period);
  const today = formatLocalDateOnly(new Date());

  return calendarSchedulingDays.slice(0, dayCount).map(({ dayIndex }) => {
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
      color: resolveCalendarSchedulingColor(event.color),
      status: event.statusTypeCode,
      draggable: false,
    },
  }));
}

function toScheduleMatrixEventItems(
  events: ReadonlyArray<CalendarEventBase>,
  userShiftEvents: ReadonlyArray<CalendarEventBase>,
): CalendarMatrixEventItem[] {
  return [...events].sort(compareAssignmentEventsForRendering).flatMap((event) => {
    const linkedShifts = resolveLinkedShiftsForAssignment(event, [...userShiftEvents]);
    const activeLinkedShifts = linkedShifts.filter((shift) => !isCancelledStatus(shift.statusTypeCode));
    const displayLinkedShifts = activeLinkedShifts.length > 0 ? activeLinkedShifts : linkedShifts;
    const status = displayLinkedShifts[0]?.statusTypeCode ?? event.statusTypeCode;

    if (linkedShifts.length > 0 && linkedShifts.every((shift) => isCancelledStatus(shift.statusTypeCode))) {
      return [];
    }

    const displayEvent = withAssignmentCapacitySlotStates(event, displayLinkedShifts);

    return [
      {
        event: displayEvent,
        display: {
          color: resolveCalendarSchedulingColor(event.color),
          status,
          draggable: false,
          action: eventHasConflicts(displayEvent) ? buildPulldownAction() : undefined,
        },
      },
    ];
  });
}

function buildPulldownAction(): CalendarMatrixActionDisplay {
  return {
    actionId: calendarSchedulingActionIds.showConflict,
    icon: mdiAlertCircle,
    ariaLabel: 'Show Conflict Details',
    type: CalendarMatrixActionType.Button,
  };
}

function eventHasConflicts(event: CalendarEventBase) {
  return isCalendarSchedulingEvent(event) && Boolean(event.metadata.conflicts?.length);
}

function compareAssignmentEventsForRendering(left: CalendarEventBase, right: CalendarEventBase) {
  const startComparison = left.start.localeCompare(right.start);
  if (startComparison !== 0) {
    return startComparison;
  }

  const leftAssignmentId = resolveAssignmentEntryIdForRendering(left);
  const rightAssignmentId = resolveAssignmentEntryIdForRendering(right);
  if (leftAssignmentId !== undefined && rightAssignmentId !== undefined) {
    return leftAssignmentId - rightAssignmentId;
  }

  if (leftAssignmentId !== undefined) {
    return -1;
  }

  if (rightAssignmentId !== undefined) {
    return 1;
  }

  return left.id.localeCompare(right.id, undefined, { numeric: true });
}

function resolveAssignmentEntryIdForRendering(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return undefined;
  }

  const assignmentEntryId = Number(event.metadata.assignmentEntryId);
  return Number.isInteger(assignmentEntryId) && assignmentEntryId > 0 ? assignmentEntryId : undefined;
}

function withAssignmentCapacitySlotStates(
  event: CalendarEventBase,
  linkedShifts: ReadonlyArray<CalendarEventBase> = [],
): CalendarEventBase {
  if (!isCalendarSchedulingEvent(event)) {
    return event;
  }

  const displayEvent: CalendarSchedulingEvent = {
    ...event,
    metadata: {
      ...event.metadata,
      capacitySlotStates: buildAssignmentCapacitySlotStates(event, linkedShifts),
      partialCoverageShifts: buildPartialCoverageShiftDetails(event, linkedShifts),
    },
  };

  return displayEvent;
}

function buildPartialCoverageShiftDetails(
  event: CalendarSchedulingEvent,
  linkedShifts: ReadonlyArray<CalendarEventBase>,
): CalendarAssignmentPartialCoverageShift[] {
  return linkedShifts
    .filter((linkedShift) => linkedShiftTimeDiffersFromAssignment(event, linkedShift))
    .map((linkedShift) => ({
      userIds: resolveLinkedAssignmentShiftUserIds(event, linkedShift),
      start: linkedShift.start,
      end: linkedShift.end,
      timeZoneId: linkedShift.timeZoneId,
    }))
    .filter((partialCoverageShift) => partialCoverageShift.userIds.length > 0);
}

function buildAssignmentCapacitySlotStates(
  event: CalendarSchedulingEvent,
  linkedShifts: ReadonlyArray<CalendarEventBase>,
): CalendarAssignmentCapacitySlotState[] {
  const capacity = Math.max(Number(event.metadata.capacity ?? 0), 0);
  const assignedCount = Math.max(Number(event.metadata.assignedCount ?? 0), 0);
  const filledCount = Math.min(assignedCount, capacity);
  const partialCount = Math.min(
    linkedShifts
      .filter((linkedShift) => linkedShiftTimeDiffersFromAssignment(event, linkedShift))
      .reduce((total, linkedShift) => total + resolveLinkedAssignmentShiftUserIds(event, linkedShift).length, 0),
    filledCount,
  );

  return Array.from({ length: capacity }, (_value, index) => {
    const slotNumber = index + 1;

    if (slotNumber <= partialCount) {
      return 'partial';
    }

    if (slotNumber <= filledCount) {
      return 'filled';
    }

    return 'empty';
  });
}

function linkedShiftTimeDiffersFromAssignment(assignmentEvent: CalendarEventBase, linkedShift: CalendarEventBase) {
  return !linkedShiftFullyCoversAssignment(assignmentEvent, linkedShift);
}

function linkedShiftFullyCoversAssignment(assignmentEvent: CalendarEventBase, linkedShift: CalendarEventBase) {
  if (!assignmentEvent.start || !assignmentEvent.end || !linkedShift.start || !linkedShift.end) {
    return false;
  }

  const assignmentStart = toDateTime(assignmentEvent.start);
  const assignmentEnd = toDateTime(assignmentEvent.end);
  const shiftStart = toDateTime(linkedShift.start);
  const shiftEnd = toDateTime(linkedShift.end);

  if (!assignmentStart.isValid || !assignmentEnd.isValid || !shiftStart.isValid || !shiftEnd.isValid) {
    return false;
  }

  return shiftStart.toMillis() <= assignmentStart.toMillis() && shiftEnd.toMillis() >= assignmentEnd.toMillis();
}

function resolveLinkedShiftUserIds(linkedShift: CalendarEventBase) {
  if (isCalendarSchedulingEvent(linkedShift)) {
    const userIds = linkedShift.metadata.userIds ?? [];
    if (userIds.length > 0) {
      return userIds;
    }
  }

  return linkedShift.resourceIds ?? [];
}

function resolveLinkedAssignmentShiftUserIds(assignmentEvent: CalendarSchedulingEvent, linkedShift: CalendarEventBase) {
  const assignedUserIds = new Set(assignmentEvent.metadata.assignedUserIds ?? []);

  if (assignedUserIds.size === 0) {
    return [];
  }

  return resolveLinkedShiftUserIds(linkedShift).filter((userId) => assignedUserIds.has(userId));
}

function resolveCalendarSchedulingColor(color?: string | null) {
  const normalized = color?.trim();

  if (!normalized) {
    return undefined;
  }

  return calendarMatrixColorMap[normalized as keyof typeof calendarMatrixColorMap] ?? normalized;
}
