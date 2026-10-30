import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { useCalendarAlertStore } from '@/modules/calendar/calendarAlertStore';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { addDays, toDateTime } from '@/utils/date';
import { CalendarModuleId } from '@/modules/calendar/calendarIdentifiers';
import type {
  CalendarDropAction,
  CalendarMatrixDropActionContext,
  CalendarCreateAction,
  CalendarMatrixCellHeaderAction,
  CalendarMatrixEventBlockAction,
  CalendarMatrixResourceAction,
  CalendarMatrixSidePanelAction,
  CalendarToolbarAction,
  CalendarViewDetailAction,
} from '@/modules/calendar/registry/calendarActionRegistryTypes';
import { calendarSchedulingActionIds } from './calendarSchedulingActionIds';
import {
  closeCalendarSchedulingConflict,
  showCalendarSchedulingAssignmentTypeCreateModal,
  showCalendarSchedulingAssignmentModal,
  showCalendarSchedulingEventDetail,
  showCalendarSchedulingPublishModal,
  showCalendarSchedulingResourceActionModal,
  showCalendarSchedulingLeaveDetail,
  toggleCalendarSchedulingConflict,
  toggleCalendarSchedulingHeaderConflict,
} from './calendarSchedulingState';
import { calendarShiftViewContribution } from './calendarShiftViewContribution';
import { calendarAssignmentViewContribution } from './calendarAssignmentViewContribution';
import { getMissingAssignmentTypePrompt } from './assignmentDefinitionPrompt';
import { getShiftSetupPrompt } from './shiftSetupPrompt';
import { isLeaveEvent } from './calendarSchedulingData';
import { parsePositiveInteger } from './calendarSchedulingShiftIds';
import {
  getSchedulingLifecycleCapabilities,
  isSchedulingCancelled,
  isSchedulingPublished,
} from './schedulingLifecycle';
import {
  defaultSchedulingTimeZoneId,
  resolveSchedulingTimeZoneFromFilters,
  resolveSchedulingTimeZoneId,
} from './schedulingTimeZone';
import {
  canAssignAssignments,
  canCreateAssignments,
  canCreateShifts,
  canCreateLeave,
  canEditAssignments,
  canEditShifts,
  canPublishSchedule,
  canViewAssignments,
  canViewShifts,
  canViewLeave,
} from './calendarSchedulingPermissions';

const assignmentShiftRequiredAlertId = 'scheduling.assignment.shift-required';
const assignmentShiftRequiredMessage = 'Create a shift for this user on this date before scheduling an assignment.';

export const calendarSchedulingCreateShiftAction: CalendarCreateAction = {
  id: calendarSchedulingActionIds.createShift,
  moduleId: CalendarModuleId.SchedulingUi,
  label: '+ Create Shift',
  isAvailable: (context, runtimeContext) =>
    context.activeViewId === calendarShiftViewContribution.id &&
    (canCreateShifts(runtimeContext) || canCreateLeave(runtimeContext)),
  getPrompt: (data, context) => getShiftSetupPrompt(data, context),
  run: (context, runtimeContext) => {
    if (!canCreateShifts(runtimeContext) && !canCreateLeave(runtimeContext)) return;
    showCalendarSchedulingResourceActionModal(undefined, context.startDate);
  },
};

export const calendarSchedulingCreateAssignmentTypeAction: CalendarCreateAction = {
  id: calendarSchedulingActionIds.createAssignmentType,
  moduleId: CalendarModuleId.SchedulingUi,
  label: '+ Create Assignment Type',
  isAvailable: (context, runtimeContext) =>
    context.activeViewId === calendarAssignmentViewContribution.id && canCreateAssignments(runtimeContext),
  getPrompt: (data, context) =>
    getMissingAssignmentTypePrompt(
      data,
      context.locationId,
      context.startDate,
      resolveSchedulingTimeZoneFromFilters(context.filters),
    ),
  run: (context, runtimeContext) => {
    if (!canCreateAssignments(runtimeContext)) return;
    showCalendarSchedulingAssignmentTypeCreateModal(context.locationId, context.startDate);
  },
};

export const calendarSchedulingPublishAction: CalendarToolbarAction = {
  id: calendarSchedulingActionIds.publishSchedule,
  label: 'Publish Schedule',
  variant: 'outlined',
  isAvailable: (context, runtimeContext) => Boolean(context.locationId) && canPublishSchedule(runtimeContext),
  run: (context, runtimeContext) => {
    if (context.locationId && canPublishSchedule(runtimeContext)) {
      showCalendarSchedulingPublishModal(context.locationId, context.startDate, addDays(context.endDate, -1));
    }
  },
};

export const calendarAddAssignmentAction: CalendarMatrixSidePanelAction = {
  id: calendarSchedulingActionIds.addAssignment,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Add assignment',
  order: 10,
  isAvailable: (context, runtimeContext) =>
    context.actionId === calendarSchedulingActionIds.addAssignment && canCreateAssignments(runtimeContext),
  execute: (context, runtimeContext) => {
    if (!canCreateAssignments(runtimeContext)) return;
    showCalendarSchedulingAssignmentModal(context.model.days[0]?.date);
  },
};

export const calendarScheduleStaffAction: CalendarMatrixSidePanelAction = {
  id: calendarSchedulingActionIds.scheduleStaff,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Schedule staff',
  order: 10,
  isAvailable: (context, runtimeContext) =>
    context.actionId === calendarSchedulingActionIds.scheduleStaff && canCreateShifts(runtimeContext),
  execute: (context, runtimeContext) => {
    if (!canCreateShifts(runtimeContext)) return;
    showCalendarSchedulingResourceActionModal(undefined, context.model.days[0]?.date);
  },
};

export const calendarAddResourceAction: CalendarMatrixResourceAction = {
  id: calendarSchedulingActionIds.addResource,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Add resource',
  order: 10,
  isAvailable: (context, runtimeContext) =>
    context.actionId === calendarSchedulingActionIds.addResource &&
    (canCreateShifts(runtimeContext) || canCreateLeave(runtimeContext)),
  execute: (context, runtimeContext) => {
    if (!canCreateShifts(runtimeContext) && !canCreateLeave(runtimeContext)) return;
    showCalendarSchedulingResourceActionModal(context.resource, context.cell?.date);
  },
};

export const calendarAddAssignmentResourceAction: CalendarMatrixResourceAction = {
  id: calendarSchedulingActionIds.addAssignmentResource,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Add assignment',
  order: 10,
  isAvailable: (context, runtimeContext) =>
    context.actionId === calendarSchedulingActionIds.addAssignmentResource && canCreateAssignments(runtimeContext),
  execute: (context, runtimeContext) => {
    if (!canCreateAssignments(runtimeContext)) return;
    showCalendarSchedulingAssignmentModal(context.cell?.date ?? context.model.days[0]?.date, {
      assignmentDefinitionId: resolveResourceAssignmentDefinitionId(context.resource) ?? undefined,
    });
  },
};

export const calendarDropAction: CalendarDropAction = {
  id: 'calendar-scheduling.drop-assignment-on-resource',
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Drop assignment type',
  order: 10,
  isAvailable: (context, runtimeContext) =>
    context.drag.itemType === 'assignment' &&
    Boolean(context.drop.date) &&
    canAssignAssignments(runtimeContext) &&
    (resolveExistingAssignmentEntryId(context)
      ? canEditAssignments(runtimeContext)
      : canCreateAssignments(runtimeContext)),
  execute: async (context, runtimeContext) => {
    const assignmentEntryId = resolveExistingAssignmentEntryId(context);
    const assignedUserId = resolveTargetUserId(context);
    const shiftEntryIds = resolveShiftEntryIds(context);
    if (
      !canAssignAssignments(runtimeContext) ||
      (assignmentEntryId ? !canEditAssignments(runtimeContext) : !canCreateAssignments(runtimeContext))
    ) {
      return;
    }
    if (assignedUserId && shiftEntryIds.length === 0) {
      useCalendarAlertStore().setAlert({
        id: assignmentShiftRequiredAlertId,
        message: assignmentShiftRequiredMessage,
        severity: 'error',
        source: CalendarModuleId.SchedulingUi,
      });
      return;
    }
    useCalendarAlertStore().clearAlert(assignmentShiftRequiredAlertId);
    showCalendarSchedulingAssignmentModal(
      context.drop.date,
      assignmentEntryId
        ? {
            mode: 'edit',
            assignmentEntryId,
            shiftEntryIds,
            assignedUserId,
          }
        : {
            assignmentDefinitionId: resolveAssignmentDefinitionId(context) ?? undefined,
            shiftEntryIds,
            assignedUserId,
          },
    );
  },
};

export const calendarDropUserOnAssignmentResourceAction: CalendarDropAction = {
  id: 'calendar-scheduling.drop-user-on-assignment-resource',
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Drop user on assignment resource',
  order: 20,
  isAvailable: (context, runtimeContext) => {
    if (context.drag.itemType !== 'user' || !context.drop.date) return false;
    if (context.drop.resourceType === 'user') {
      if (context.drop.resourceId !== context.drag.itemId) return false;
      return resolveExistingShiftForDroppedUser(context)
        ? canEditShifts(runtimeContext)
        : canCreateShifts(runtimeContext);
    }
    if (context.drop.resourceType !== 'assignment') return false;
    if (!resolveTargetAssignmentDefinitionId(context)) return false;
    const assignmentEvents = resolveAssignmentEvents(context);
    const existingShift = resolveExistingShiftForDroppedUser(context);
    if (!existingShift) return canCreateShifts(runtimeContext);
    return (
      canAssignAssignments(runtimeContext) &&
      (assignmentEvents.length ? canEditAssignments(runtimeContext) : canCreateAssignments(runtimeContext))
    );
  },
  execute: (context, runtimeContext) => {
    if (!calendarDropUserOnAssignmentResourceAction.isAvailable?.(context, runtimeContext)) return;
    const resource = resolveUserResource(context);
    const existingShift = resolveExistingShiftForDroppedUser(context);
    if (context.drop.resourceType === 'user') {
      if (existingShift) {
        showCalendarSchedulingEventDetail(existingShift, { initialOpenScope: 'event', initialTab: 'edit' });
      } else {
        showCalendarSchedulingResourceActionModal(resource, context.drop.date);
      }
      return;
    }
    const assignmentEvents = resolveAssignmentEvents(context);
    if (assignmentEvents.length > 1) {
      useCalendarAlertStore().setAlert({
        id: 'scheduling.assignment.ambiguous-drop',
        message: 'Multiple assignments exist on this day. Select the intended assignment manually.',
        severity: 'warning',
        source: CalendarModuleId.SchedulingUi,
      });
      return;
    }
    const assignmentEvent = assignmentEvents[0];
    const assignmentEntryId = assignmentEvent ? resolveAssignmentEntryId(assignmentEvent) : undefined;
    if (assignmentEvent && !getSchedulingLifecycleCapabilities(assignmentEvent.statusTypeCode).canEdit) {
      if (assignmentEntryId && canViewAssignments(runtimeContext)) {
        showCalendarSchedulingAssignmentModal(context.drop.date, {
          mode: 'view',
          assignmentEntryId,
          assignmentSeriesId: resolveAssignmentSeriesId(assignmentEvent) ?? undefined,
        });
      }
      return;
    }
    if (!existingShift) {
      showCalendarSchedulingResourceActionModal(resource, context.drop.date, {
        assignmentEntryId: assignmentEntryId ?? undefined,
        assignmentEvents,
        missingShiftWarning: true,
      });
      return;
    }
    const shiftEntryId = resolveShiftEntryId(existingShift);
    if (!shiftEntryId || (assignmentEvent && !assignmentEntryId)) return;
    useCalendarAlertStore().clearAlert(assignmentShiftRequiredAlertId);
    showCalendarSchedulingAssignmentModal(context.drop.date, {
      mode: assignmentEvent ? 'edit' : 'create',
      editScope: assignmentEvent ? 'event' : undefined,
      assignmentEntryId: assignmentEntryId ?? undefined,
      assignmentSeriesId: assignmentEvent ? (resolveAssignmentSeriesId(assignmentEvent) ?? undefined) : undefined,
      ...(assignmentEvent ? {} : { assignmentDefinitionId: resolveTargetAssignmentDefinitionId(context) }),
      shiftEntryIds: [shiftEntryId],
      assignedUserId: context.drag.itemId,
      draggedUserOnly: true,
    });
  },
};

export const calendarSchedulingShowConflictAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.showConflict,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Show conflict',
  order: 20,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.showConflict && isSchedulingMatrixEvent(context.event),
  execute: (context) => {
    toggleCalendarSchedulingConflict(context.event.id);
  },
};

export const calendarSchedulingResolveConflictAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.resolveConflict,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Resolve conflict',
  order: 30,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.resolveConflict && isSchedulingMatrixEvent(context.event),
  execute: (_context) => {
    closeCalendarSchedulingConflict();
  },
};

export const calendarSchedulingEventDetailAction: CalendarViewDetailAction = {
  id: 'calendar-scheduling.event-detail.modal',
  moduleId: CalendarModuleId.SchedulingUi,
  isAvailable: (context) =>
    isLeaveEvent(context.event)
      ? canViewLeave(context.runtimeContext)
      : (context.event.sourceModule === CalendarModuleId.SchedulingUi ||
          context.event.sourceModule === CalendarModuleId.Scheduling ||
          isAssignmentEvent(context.event)) &&
        (isAssignmentEvent(context.event)
          ? canViewAssignments(context.runtimeContext)
          : canViewShifts(context.runtimeContext)),
  run: (context) => {
    if (isLeaveEvent(context.event)) {
      if (canViewLeave(context.runtimeContext)) {
        showCalendarSchedulingLeaveDetail(context.event);
      }
      return;
    }

    if (
      (isAssignmentEvent(context.event) && !canViewAssignments(context.runtimeContext)) ||
      (!isAssignmentEvent(context.event) && !canViewShifts(context.runtimeContext))
    ) {
      return;
    }

    if (isAssignmentEvent(context.event)) {
      const assignmentEntryId = resolveAssignmentEntryId(context.event);
      if (assignmentEntryId) {
        const eventDate = toDateTime(
          context.event.start,
          resolveSchedulingTimeZoneId(
            context.event.timeZoneId,
            resolveSchedulingTimeZoneFromFilters(context.queryContext?.filters ?? {}),
          ),
        ).toISODate();
        if (!eventDate) {
          return;
        }

        useCalendarStore().clearSelectedEvent();
        showCalendarSchedulingAssignmentModal(eventDate, {
          mode: 'view',
          assignmentEntryId,
          assignmentSeriesId: resolveAssignmentSeriesId(context.event) ?? undefined,
        });
      }
      return;
    }

    showCalendarSchedulingEventDetail(context.event);
  },
};

export const calendarSchedulingHeaderDetailAction: CalendarMatrixCellHeaderAction = {
  id: calendarSchedulingActionIds.viewHeaderDetails,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'View header details',
  order: 10,
  isAvailable: (context, runtimeContext) =>
    context.actionId === calendarSchedulingActionIds.viewHeaderDetails &&
    isCalendarEventBase(context.header.payload) &&
    canViewShifts(runtimeContext),
  execute: (context, runtimeContext) => {
    if (canViewShifts(runtimeContext) && isCalendarEventBase(context.header.payload)) {
      showCalendarSchedulingEventDetail(context.header.payload);
    }
  },
};

export const calendarSchedulingHeaderLeaveDetailAction: CalendarMatrixCellHeaderAction = {
  id: calendarSchedulingActionIds.viewLeaveDetails,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'View leave details',
  order: 10,
  isAvailable: (context, runtimeContext) =>
    context.actionId === calendarSchedulingActionIds.viewLeaveDetails &&
    isCalendarEventBase(context.header.payload) &&
    canViewLeave(runtimeContext),
  execute: (context, runtimeContext) => {
    if (canViewLeave(runtimeContext) && isCalendarEventBase(context.header.payload)) {
      showCalendarSchedulingLeaveDetail(context.header.payload);
    }
  },
};

export const calendarSchedulingHeaderShowConflictAction: CalendarMatrixCellHeaderAction = {
  id: calendarSchedulingActionIds.showConflict,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Show header conflict',
  order: 20,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.showConflict && isCalendarEventBase(context.header.payload),
  execute: (context) => {
    if (isCalendarEventBase(context.header.payload)) {
      toggleCalendarSchedulingHeaderConflict(context.cell, context.header.payload.id);
    }
  },
};

export const calendarSchedulingHeaderResolveConflictAction: CalendarMatrixCellHeaderAction = {
  id: calendarSchedulingActionIds.resolveConflict,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Resolve header conflict',
  order: 30,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.resolveConflict && isCalendarEventBase(context.header.payload),
  execute: (context) => {
    if (isCalendarEventBase(context.header.payload)) {
      closeCalendarSchedulingConflict();
    }
  },
};

function isCalendarEventBase(value: unknown): value is CalendarEventBase {
  return (
    typeof value === 'object' &&
    value !== null &&
    'id' in value &&
    'type' in value &&
    'sourceModule' in value &&
    'title' in value &&
    'start' in value
  );
}

function isSchedulingMatrixEvent(event: CalendarEventBase) {
  return (
    event.sourceModule === CalendarModuleId.SchedulingUi ||
    event.sourceModule === CalendarModuleId.Scheduling ||
    isAssignmentEvent(event)
  );
}

function resolveAssignmentDefinitionId(context: { drag: { payload?: unknown } }) {
  const payload = context.drag.payload;
  if (typeof payload !== 'object' || payload === null) return undefined;
  return parsePositiveInteger((payload as { assignmentDefinitionId?: unknown }).assignmentDefinitionId);
}

function resolveResourceAssignmentDefinitionId(resource: unknown) {
  if (typeof resource !== 'object' || resource === null) return undefined;
  const resourceId = (resource as { id?: unknown }).id;
  const fromId = typeof resourceId === 'string' ? /^assignment-definition-(\d+)$/.exec(resourceId)?.[1] : undefined;
  return parsePositiveInteger((resource as { assignmentDefinitionId?: unknown }).assignmentDefinitionId ?? fromId);
}

function resolveShiftEntryIds(context: CalendarMatrixDropActionContext) {
  const matchingCell = context.model.cells.find(
    (cell) => cell.resourceId === context.drop.resourceId && cell.date === context.drop.date,
  );
  const targetUserId = resolveTargetUserId(context);
  const assignmentLocationId = resolveAssignmentLocationId(context);
  const ids =
    matchingCell?.headers
      ?.map((header) => {
        if (
          !isCalendarEventBase(header.payload) ||
          (targetUserId && !eventIncludesUser(header.payload, targetUserId)) ||
          (assignmentLocationId && header.payload.locationId && header.payload.locationId !== assignmentLocationId)
        ) {
          return undefined;
        }
        return resolveShiftEntryId(header.payload);
      })
      .filter((id): id is number => typeof id === 'number') ?? [];
  return [...new Set(ids)];
}

function resolveTargetUserId(context: CalendarMatrixDropActionContext) {
  const targetResource = context.model.primaryColumn.resources.find(
    (resource) => resource.id === context.drop.resourceId,
  );
  return context.drop.resourceType === 'user' || targetResource?.type === 'user' ? context.drop.resourceId : undefined;
}

function resolveExistingAssignmentEntryId(context: CalendarMatrixDropActionContext) {
  const definitionId = resolveAssignmentDefinitionId(context);
  const modelAssignmentEvents =
    typeof context.model.payload === 'object' &&
    context.model.payload !== null &&
    Array.isArray((context.model.payload as { assignmentEvents?: unknown }).assignmentEvents)
      ? ((context.model.payload as { assignmentEvents: CalendarEventBase[] }).assignmentEvents ?? [])
      : [];
  const renderedAssignmentEvents = context.model.cells.flatMap((cell) =>
    cell.groups.flatMap((group) => group.events.map((item) => item.event).filter(isAssignmentEvent)),
  );
  const matchingEvent = [...modelAssignmentEvents, ...renderedAssignmentEvents].find(
    (event) =>
      isAssignmentEvent(event) &&
      !isSchedulingCancelled(event.statusTypeCode) &&
      !isSchedulingPublished(event.statusTypeCode) &&
      (!definitionId || resolveEventAssignmentDefinitionId(event) === definitionId) &&
      assignmentEntryMatchesDate(event, context.drop.date, context.model.timeZone),
  );

  return matchingEvent ? resolveAssignmentEntryId(matchingEvent) : undefined;
}

function assignmentEntryMatchesDate(entry: unknown, date?: string, timeZone?: string) {
  if (!date || typeof entry !== 'object' || entry === null) {
    return false;
  }

  const value =
    (entry as { start?: unknown; startAtUtc?: unknown }).start ?? (entry as { startAtUtc?: unknown }).startAtUtc;
  if (typeof value !== 'string') {
    return false;
  }

  const parsed = toDateTime(value, timeZone || defaultSchedulingTimeZoneId);
  return parsed.isValid && parsed.toISODate() === date;
}

function resolveEventAssignmentDefinitionId(event: CalendarEventBase) {
  return parsePositiveInteger(
    (event as { metadata?: { assignmentDefinitionId?: unknown } }).metadata?.assignmentDefinitionId,
  );
}

function resolveAssignmentLocationId(context: CalendarMatrixDropActionContext) {
  const payload = context.drag.payload;
  return parsePositiveInteger(
    typeof payload === 'object' && payload !== null ? (payload as { locationId?: unknown }).locationId : undefined,
  );
}

function resolveUserResource(context: CalendarMatrixDropActionContext) {
  const title =
    typeof context.drag.payload === 'object' && context.drag.payload !== null
      ? (context.drag.payload as { title?: unknown }).title
      : undefined;
  return {
    id: context.drag.itemId,
    type: 'user',
    title: typeof title === 'string' && title.trim() ? title : context.drag.itemId,
  };
}

function resolveAssignmentEvents(context: CalendarMatrixDropActionContext) {
  const definitionId = resolveTargetAssignmentDefinitionId(context);
  if (!definitionId) return [];
  const targetResource = context.model.primaryColumn.resources.find(
    (resource) => resource.id === context.drop.resourceId,
  );
  const locationId = parsePositiveInteger((targetResource as { locationId?: unknown } | undefined)?.locationId);
  const cell = context.model.cells.find(
    (candidate) => candidate.resourceId === context.drop.resourceId && candidate.date === context.drop.date,
  );
  const events = cell?.groups.flatMap((group) => group.events.map((item) => item.event)) ?? [];
  return [
    ...new Map(
      events
        .filter(
          (event) =>
            isAssignmentEvent(event) &&
            !isSchedulingCancelled(event.statusTypeCode) &&
            resolveEventAssignmentDefinitionId(event) === definitionId &&
            assignmentEntryMatchesDate(event, context.drop.date, context.model.timeZone) &&
            (!locationId || event.locationId == null || event.locationId === locationId),
        )
        .map((event) => [resolveAssignmentEntryId(event) ?? event.id, event]),
    ).values(),
  ];
}

function resolveTargetAssignmentDefinitionId(context: CalendarMatrixDropActionContext) {
  const resource = context.model.primaryColumn.resources.find((candidate) => candidate.id === context.drop.resourceId);
  return resolveResourceAssignmentDefinitionId(resource ?? { id: context.drop.resourceId }) ?? undefined;
}

function resolveExistingShiftForDroppedUser(context: CalendarMatrixDropActionContext) {
  const targetResource = context.model.primaryColumn.resources.find(
    (resource) => resource.id === context.drop.resourceId,
  );
  const assignmentLocationId = parsePositiveInteger(
    (targetResource as { locationId?: unknown } | undefined)?.locationId,
  );
  const shiftEvents = context.model.cells
    .filter((cell) => cell.date === context.drop.date)
    .flatMap((cell) => [
      ...getPayloadShiftEvents(cell.payload),
      ...(cell.headers?.map((header) => header.payload).filter(isCalendarEventBase) ?? []),
    ]);

  return shiftEvents.find(
    (event) =>
      isShiftEvent(event) &&
      !isSchedulingCancelled(event.statusTypeCode) &&
      eventIncludesUser(event, context.drag.itemId) &&
      assignmentEntryMatchesDate(event, context.drop.date, context.model.timeZone) &&
      (!assignmentLocationId || event.locationId == null || event.locationId === assignmentLocationId),
  );
}

function getPayloadShiftEvents(payload: unknown): CalendarEventBase[] {
  if (typeof payload !== 'object' || payload === null) {
    return [];
  }

  const shiftEvents = (payload as { shiftEvents?: unknown }).shiftEvents;
  return Array.isArray(shiftEvents) ? shiftEvents.filter(isCalendarEventBase) : [];
}

function resolveAssignmentEntryId(event: CalendarEventBase) {
  return parsePositiveInteger((event as { metadata?: { assignmentEntryId?: unknown } }).metadata?.assignmentEntryId);
}

function resolveAssignmentSeriesId(event: CalendarEventBase) {
  return parsePositiveInteger((event as { metadata?: { assignmentSeriesId?: unknown } }).metadata?.assignmentSeriesId);
}

function resolveShiftEntryId(event: CalendarEventBase) {
  return parsePositiveInteger((event as { metadata?: { shiftEntryId?: unknown } }).metadata?.shiftEntryId);
}

function eventIncludesUser(event: CalendarEventBase, userId: string) {
  const metadata = (event as { metadata?: { assignedUserIds?: unknown; userIds?: unknown; userId?: unknown } })
    .metadata;
  const userIds = [metadata?.assignedUserIds, metadata?.userIds].flatMap((ids) =>
    Array.isArray(ids) ? ids.filter((id): id is string => typeof id === 'string') : [],
  );
  return event.resourceIds?.includes(userId) || userIds.includes(userId) || metadata?.userId === userId;
}

function isShiftEvent(event: CalendarEventBase) {
  return event.type === 'scheduling.shift' || event.eventTypeCode === 'shift' || Boolean(resolveShiftEntryId(event));
}

function isAssignmentEvent(event: CalendarEventBase) {
  return (
    event.type === 'scheduling.assignment' ||
    event.eventTypeCode === 'assignment' ||
    Boolean(resolveAssignmentEntryId(event))
  );
}
