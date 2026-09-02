import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { DateTime } from 'luxon';
import { CalendarModuleId } from '@/modules/calendar/calendarIdentifiers';
import type {
  CalendarDropAction,
  CalendarMatrixDropActionContext,
  CalendarCreateAction,
  CalendarMatrixCellHeaderAction,
  CalendarMatrixEventBlockAction,
  CalendarMatrixResourceAction,
  CalendarMatrixSidePanelAction,
  CalendarViewDetailAction,
} from '@/modules/calendar/registry/calendarActionRegistryTypes';
import { calendarSchedulingActionIds } from './calendarSchedulingActionIds';
import {
  closeCalendarSchedulingConflict,
  showCalendarSchedulingAssignmentModal,
  showCalendarSchedulingExistingShiftChoice,
  showCalendarSchedulingEventActionModal,
  showCalendarSchedulingEventDetail,
  showCalendarSchedulingResourceActionModal,
  toggleCalendarSchedulingConflict,
} from './calendarSchedulingState';
import { calendarShiftViewContribution } from './calendarShiftViewContribution';
import {
  isAssignmentEvent,
  isShiftEvent,
  parsePositiveInteger,
  resolveAssignmentEntryId,
  resolveAssignmentSeriesId,
  resolveShiftEntryId,
} from './calendarSchedulingShiftIds';
import { isSchedulingCancelled } from './schedulingLifecycle';

export const calendarSchedulingCreateShiftAction: CalendarCreateAction = {
  id: calendarSchedulingActionIds.createShift,
  moduleId: CalendarModuleId.SchedulingUi,
  label: '+ Create Shift',
  isAvailable: (context) => context.activeViewId === calendarShiftViewContribution.id,
  run: (context) => {
    showCalendarSchedulingResourceActionModal(undefined, context.startDate);
  },
};

export const calendarAddAssignmentAction: CalendarMatrixSidePanelAction = {
  id: calendarSchedulingActionIds.addAssignment,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Add assignment',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.addAssignment,
  execute: (context) => {
    showCalendarSchedulingAssignmentModal(context.model.days[0]?.date);
  },
};

export const calendarScheduleStaffAction: CalendarMatrixSidePanelAction = {
  id: calendarSchedulingActionIds.scheduleStaff,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Schedule staff',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.scheduleStaff,
  execute: (context) => {
    showCalendarSchedulingResourceActionModal(undefined, context.model.days[0]?.date);
  },
};

export const calendarAddResourceAction: CalendarMatrixResourceAction = {
  id: calendarSchedulingActionIds.addResource,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Add resource',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.addResource,
  execute: (context) => {
    showCalendarSchedulingResourceActionModal(context.resource, context.cell?.date);
  },
};

export const calendarAddAssignmentResourceAction: CalendarMatrixResourceAction = {
  id: calendarSchedulingActionIds.addAssignmentResource,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Add assignment',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.addAssignmentResource,
  execute: (context) => {
    showCalendarSchedulingAssignmentModal(context.cell?.date ?? context.model.days[0]?.date, {
      assignmentDefinitionId: resolveResourceAssignmentDefinitionId(context.resource),
    });
  },
};

export const calendarDropAction: CalendarDropAction = {
  id: 'calendar-scheduling.drop-assignment-on-resource',
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Drop assignment type',
  order: 10,
  isAvailable: (context) => context.drag.itemType === 'assignment' && Boolean(context.drop.date),
  execute: async (context) => {
    const assignmentEntryId = resolveExistingAssignmentEntryId(context);
    showCalendarSchedulingAssignmentModal(
      context.drop.date,
      assignmentEntryId
        ? {
            mode: 'edit',
            assignmentEntryId,
            shiftEntryIds: resolveShiftEntryIds(context),
          }
        : {
            assignmentDefinitionId: resolveAssignmentDefinitionId(context),
            shiftEntryIds: resolveShiftEntryIds(context),
          },
    );
  },
};

export const calendarDropUserOnAssignmentResourceAction: CalendarDropAction = {
  id: 'calendar-scheduling.drop-user-on-assignment-resource',
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Drop user on assignment resource',
  order: 20,
  isAvailable: (context) =>
    context.drag.itemType === 'user' && context.drop.resourceType === 'assignment' && Boolean(context.drop.date),
  execute: async (context) => {
    const assignmentEvents = resolveAssignmentEvents(context);
    const resource = resolveUserResource(context);
    const assignmentEntryId = assignmentEvents.length === 1 ? resolveAssignmentEntryId(assignmentEvents[0]) : undefined;
    const existingShift = resolveExistingShiftForDroppedUser(context);

    if (existingShift) {
      showCalendarSchedulingExistingShiftChoice({
        shiftEvent: existingShift,
        resource,
        date: context.drop.date,
        assignmentEntryId,
        assignmentEvents,
      });
      return;
    }

    showCalendarSchedulingResourceActionModal(resource, context.drop.date, { assignmentEntryId, assignmentEvents });
  },
};

export const calendarEventBlockAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.addOnEvent,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Add on event',
  order: 10,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.addOnEvent &&
    context.event.sourceModule === CalendarModuleId.SchedulingUi,
  execute: (context) => {
    showCalendarSchedulingEventActionModal(context.event);
  },
};

export const calendarSchedulingShowConflictAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.showConflict,
  moduleId: CalendarModuleId.SchedulingUi,
  label: 'Show conflict',
  order: 20,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.showConflict &&
    context.event.sourceModule === CalendarModuleId.SchedulingUi,
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
    context.actionId === calendarSchedulingActionIds.resolveConflict &&
    context.event.sourceModule === CalendarModuleId.SchedulingUi,
  execute: (_context) => {
    closeCalendarSchedulingConflict();
  },
};

export const calendarSchedulingEventDetailAction: CalendarViewDetailAction = {
  id: 'calendar-scheduling.event-detail.modal',
  moduleId: CalendarModuleId.SchedulingUi,
  isAvailable: (context) =>
    context.event.sourceModule === CalendarModuleId.SchedulingUi ||
    context.event.sourceModule === CalendarModuleId.Scheduling ||
    isAssignmentEvent(context.event),
  run: (context) => {
    if (isAssignmentEvent(context.event)) {
      const assignmentEntryId = resolveAssignmentEntryId(context.event);
      if (assignmentEntryId) {
        useCalendarStore().clearSelectedEvent();
        showCalendarSchedulingAssignmentModal(context.event.start.slice(0, 10), {
          mode: 'view',
          assignmentEntryId,
          assignmentSeriesId: resolveAssignmentSeriesId(context.event),
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
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.viewHeaderDetails && isCalendarEventBase(context.header.payload),
  execute: (context) => {
    if (isCalendarEventBase(context.header.payload)) {
      showCalendarSchedulingEventDetail(context.header.payload);
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
      toggleCalendarSchedulingConflict(context.header.payload.id);
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

function resolveAssignmentDefinitionId(context: { drag: { payload?: unknown } }) {
  const payload = context.drag.payload;
  if (typeof payload !== 'object' || payload === null) return undefined;
  return parsePositiveInteger((payload as { assignmentDefinitionId?: unknown }).assignmentDefinitionId) ?? undefined;
}

function resolveResourceAssignmentDefinitionId(resource: unknown) {
  if (typeof resource !== 'object' || resource === null) return undefined;
  const resourceId = (resource as { id?: unknown }).id;
  const fromId = typeof resourceId === 'string' ? /^assignment-definition-(\d+)$/.exec(resourceId)?.[1] : undefined;
  return (
    parsePositiveInteger((resource as { assignmentDefinitionId?: unknown }).assignmentDefinitionId ?? fromId) ??
    undefined
  );
}

function resolveShiftEntryIds(context: CalendarMatrixDropActionContext) {
  const matchingCell = context.model.cells.find(
    (cell) => cell.resourceId === context.drop.resourceId && cell.date === context.drop.date,
  );
  const targetResource = context.model.primaryColumn.resources.find(
    (resource) => resource.id === context.drop.resourceId,
  );
  const targetUserId =
    context.drop.resourceType === 'user' || targetResource?.type === 'user' ? context.drop.resourceId : undefined;
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

function resolveExistingAssignmentEntryId(context: CalendarMatrixDropActionContext) {
  const definitionId = resolveAssignmentDefinitionId(context);
  const payload = context.drag.payload;
  const payloadEntries =
    typeof payload === 'object' && payload !== null && Array.isArray((payload as { entries?: unknown }).entries)
      ? ((payload as { entries: unknown[] }).entries ?? [])
      : [];
  const matchingPayloadEntry = payloadEntries.find((entry) =>
    assignmentEntryMatchesDate(entry, context.drop.date, context.model.timeZone),
  );
  const payloadEntryId = parsePositiveInteger(
    typeof matchingPayloadEntry === 'object' && matchingPayloadEntry !== null
      ? (matchingPayloadEntry as { id?: unknown }).id
      : undefined,
  );
  if (payloadEntryId) {
    return payloadEntryId;
  }

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

  const parsed = DateTime.fromISO(value, { setZone: true }).setZone(timeZone || 'America/Vancouver');
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
  const cell = context.model.cells.find(
    (candidate) => candidate.resourceId === context.drop.resourceId && candidate.date === context.drop.date,
  );
  return cell?.groups.flatMap((group) => group.events.map((item) => item.event)).filter(isAssignmentEvent) ?? [];
}

function resolveExistingShiftForDroppedUser(context: CalendarMatrixDropActionContext) {
  const cell = context.model.cells.find(
    (candidate) => candidate.resourceId === context.drop.resourceId && candidate.date === context.drop.date,
  );
  const targetResource = context.model.primaryColumn.resources.find(
    (resource) => resource.id === context.drop.resourceId,
  );
  const assignmentLocationId = parsePositiveInteger(
    (targetResource as { locationId?: unknown } | undefined)?.locationId,
  );
  const shiftEvents = [
    ...getPayloadShiftEvents(cell?.payload),
    ...(cell?.headers?.map((header) => header.payload).filter(isCalendarEventBase) ?? []),
  ];

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

function eventIncludesUser(event: CalendarEventBase, userId: string) {
  const metadata = (event as { metadata?: { assignedUserIds?: unknown; userIds?: unknown; userId?: unknown } })
    .metadata;
  const userIds = [metadata?.assignedUserIds, metadata?.userIds].flatMap((ids) =>
    Array.isArray(ids) ? ids.filter((id): id is string => typeof id === 'string') : [],
  );
  return event.resourceIds?.includes(userId) || userIds.includes(userId) || metadata?.userId === userId;
}
