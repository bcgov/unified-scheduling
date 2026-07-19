import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
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
  showCalendarSchedulingEventActionModal,
  showCalendarSchedulingEventDetail,
  showCalendarSchedulingResourceActionModal,
  toggleCalendarSchedulingConflict,
} from './calendarSchedulingState';
import { calendarShiftViewContribution } from './calendarShiftViewContribution';

export const calendarSchedulingCreateShiftAction: CalendarCreateAction = {
  id: calendarSchedulingActionIds.createShift,
  moduleId: 'calendar-scheduling',
  label: '+ Create Shift',
  isAvailable: (context) => context.activeViewId === calendarShiftViewContribution.id,
  run: (context) => {
    showCalendarSchedulingResourceActionModal(undefined, context.startDate);
  },
};

export const calendarAddAssignmentAction: CalendarMatrixSidePanelAction = {
  id: calendarSchedulingActionIds.addAssignment,
  moduleId: 'calendar-scheduling',
  label: 'Add Assignment',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.addAssignment,
  execute: (context) => {
    showCalendarSchedulingAssignmentModal(context.model.days[0]?.date);
  },
};

export const calendarScheduleStaffAction: CalendarMatrixSidePanelAction = {
  id: calendarSchedulingActionIds.scheduleStaff,
  moduleId: 'calendar-scheduling',
  label: 'Schedule Staff',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.scheduleStaff,
  execute: (context) => {
    showCalendarSchedulingResourceActionModal(undefined, context.model.days[0]?.date);
  },
};

export const calendarAddResourceAction: CalendarMatrixResourceAction = {
  id: calendarSchedulingActionIds.addResource,
  moduleId: 'calendar-scheduling',
  label: 'Add Resource',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.addResource,
  execute: (context) => {
    showCalendarSchedulingResourceActionModal(context.resource, context.cell?.date);
  },
};

export const calendarAddAssignmentResourceAction: CalendarMatrixResourceAction = {
  id: calendarSchedulingActionIds.addAssignmentResource,
  moduleId: 'calendar-scheduling',
  label: 'Add Assignment',
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
  moduleId: 'calendar-scheduling',
  label: 'Drop Assignment type',
  order: 10,
  isAvailable: (context) => context.drag.itemType === 'assignment' && Boolean(context.drop.date),
  execute: async (context) => {
    showCalendarSchedulingAssignmentModal(context.drop.date, {
      assignmentDefinitionId: resolveAssignmentDefinitionId(context),
      shiftEntryIds: resolveShiftEntryIds(context),
    });
  },
};

export const calendarDropUserOnAssignmentResourceAction: CalendarDropAction = {
  id: 'calendar-scheduling.drop-user-on-assignment-resource',
  moduleId: 'calendar-scheduling',
  label: 'Drop User On Assignment Resource',
  order: 20,
  isAvailable: (context) =>
    context.drag.itemType === 'user' && context.drop.resourceType === 'assignment' && Boolean(context.drop.date),
  execute: async (context) => {
    const assignmentEvents = resolveAssignmentEvents(context);
    showCalendarSchedulingResourceActionModal(resolveUserResource(context), context.drop.date, {
      assignmentEntryId: assignmentEvents.length === 1 ? resolveAssignmentEntryId(assignmentEvents[0]) : undefined,
      assignmentEvents,
    });
  },
};

export const calendarEventBlockAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.addOnEvent,
  moduleId: 'calendar-scheduling',
  label: 'Add On Event',
  order: 10,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.addOnEvent && context.event.sourceModule === 'calendar-scheduling',
  execute: (context) => {
    showCalendarSchedulingEventActionModal(context.event);
  },
};

export const calendarSchedulingShowConflictAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.showConflict,
  moduleId: 'calendar-scheduling',
  label: 'Show Conflict',
  order: 20,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.showConflict &&
    context.event.sourceModule === 'calendar-scheduling',
  execute: (context) => {
    toggleCalendarSchedulingConflict(context.event.id);
  },
};

export const calendarSchedulingResolveConflictAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.resolveConflict,
  moduleId: 'calendar-scheduling',
  label: 'Resolve Conflict',
  order: 30,
  isAvailable: (context) =>
    context.actionId === calendarSchedulingActionIds.resolveConflict &&
    context.event.sourceModule === 'calendar-scheduling',
  execute: (_context) => {
    closeCalendarSchedulingConflict();
  },
};

export const calendarSchedulingEventDetailAction: CalendarViewDetailAction = {
  id: 'calendar-scheduling.event-detail.modal',
  moduleId: 'calendar-scheduling',
  isAvailable: (context) =>
    context.event.sourceModule === 'calendar-scheduling' ||
    context.event.sourceModule === 'calendar-assignment' ||
    context.event.sourceModule === 'scheduling' ||
    isAssignmentEvent(context.event),
  run: (context) => {
    if (isAssignmentEvent(context.event)) {
      const assignmentEntryId = resolveAssignmentEntryId(context.event);
      if (assignmentEntryId) {
        showCalendarSchedulingAssignmentModal(context.event.start.slice(0, 10), {
          mode: 'view',
          assignmentEntryId,
        });
      }
      return;
    }

    showCalendarSchedulingEventDetail(context.event);
  },
};

export const calendarSchedulingHeaderDetailAction: CalendarMatrixCellHeaderAction = {
  id: calendarSchedulingActionIds.viewHeaderDetails,
  moduleId: 'calendar-scheduling',
  label: 'View Header Details',
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
  moduleId: 'calendar-scheduling',
  label: 'Show Header Conflict',
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
  moduleId: 'calendar-scheduling',
  label: 'Resolve Header Conflict',
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
  if (typeof payload !== 'object' || payload === null) {
    return undefined;
  }

  const parsed = Number((payload as { assignmentDefinitionId?: unknown }).assignmentDefinitionId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function resolveResourceAssignmentDefinitionId(resource: unknown) {
  if (typeof resource !== 'object' || resource === null) {
    return undefined;
  }

  const assignmentDefinitionId = (resource as { assignmentDefinitionId?: unknown }).assignmentDefinitionId;
  const parsed = Number(assignmentDefinitionId ?? parseAssignmentDefinitionIdFromResourceId(resource));
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function parseAssignmentDefinitionIdFromResourceId(resource: object) {
  const resourceId = (resource as { id?: unknown }).id;
  if (typeof resourceId !== 'string') {
    return undefined;
  }

  const match = /^assignment-definition-(\d+)$/.exec(resourceId);
  return match ? Number(match[1]) : undefined;
}

function resolveShiftEntryIds(context: CalendarMatrixDropActionContext) {
  const matchingCell = context.model.cells.find(
    (cell) => cell.resourceId === context.drop.resourceId && cell.date === context.drop.date,
  );
  const targetUserId = resolveTargetUserId(context);
  const assignmentLocationId = resolveAssignmentLocationId(context);
  const shiftEntryIds =
    matchingCell?.headers
      ?.map((header) => {
        if (!isCalendarEventBase(header.payload)) {
          return undefined;
        }

        if (!eventMatchesLocation(header.payload, assignmentLocationId)) {
          return undefined;
        }

        if (targetUserId && !eventIncludesUser(header.payload, targetUserId)) {
          return undefined;
        }

        return resolveShiftEntryId(header.payload);
      })
      .filter((shiftEntryId): shiftEntryId is number => typeof shiftEntryId === 'number') ?? [];

  return [...new Set(shiftEntryIds)];
}

function resolveTargetUserId(context: CalendarMatrixDropActionContext) {
  if (context.drop.resourceType === 'user') {
    return context.drop.resourceId;
  }

  const targetResource = context.model.primaryColumn.resources.find(
    (resource) => resource.id === context.drop.resourceId,
  );
  return targetResource?.type === 'user' ? targetResource.id : undefined;
}

function resolveAssignmentLocationId(context: CalendarMatrixDropActionContext) {
  const payloadLocationId = parsePositiveNumber(
    typeof context.drag.payload === 'object' && context.drag.payload !== null
      ? (context.drag.payload as { locationId?: unknown }).locationId
      : undefined,
  );

  if (payloadLocationId) {
    return payloadLocationId;
  }

  const targetResource = context.model.primaryColumn.resources.find((resource) => resource.id === context.drag.itemId);
  return parsePositiveNumber((targetResource as { locationId?: unknown } | undefined)?.locationId);
}

function eventMatchesLocation(event: CalendarEventBase, locationId?: number) {
  if (!locationId) {
    return true;
  }

  const eventLocationId = parsePositiveNumber(event.locationId);
  return !eventLocationId || eventLocationId === locationId;
}

function parsePositiveNumber(value: unknown) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function eventIncludesUser(event: CalendarEventBase, userId: string) {
  const metadata = (event as { metadata?: { assignedUserIds?: unknown; userIds?: unknown; userId?: unknown } })
    .metadata;
  const metadataUserIds = Array.isArray(metadata?.userIds)
    ? metadata.userIds.filter((candidate): candidate is string => typeof candidate === 'string')
    : [];
  const metadataAssignedUserIds = Array.isArray(metadata?.assignedUserIds)
    ? metadata.assignedUserIds.filter((candidate): candidate is string => typeof candidate === 'string')
    : [];

  return (
    event.resourceIds?.includes(userId) ||
    metadataUserIds.includes(userId) ||
    metadataAssignedUserIds.includes(userId) ||
    metadata?.userId === userId
  );
}

function resolveUserResource(context: CalendarMatrixDropActionContext) {
  const payload = context.drag.payload;
  const payloadTitle =
    typeof payload === 'object' && payload !== null ? (payload as { title?: unknown }).title : undefined;

  return {
    id: context.drag.itemId,
    type: 'user',
    title: typeof payloadTitle === 'string' && payloadTitle.trim() ? payloadTitle : context.drag.itemId,
  };
}

function resolveAssignmentEvents(context: CalendarMatrixDropActionContext) {
  const matchingCell = context.model.cells.find(
    (cell) => cell.resourceId === context.drop.resourceId && cell.date === context.drop.date,
  );

  return (
    matchingCell?.groups
      .flatMap((group) => group.events.map((item) => item.event))
      .filter((event) => isAssignmentEvent(event) && Boolean(resolveAssignmentEntryId(event))) ?? []
  );
}

function resolveAssignmentEntryId(event: CalendarEventBase) {
  const metadata = (event as { metadata?: { assignmentEntryId?: unknown } }).metadata;
  const parsed = Number(metadata?.assignmentEntryId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function isAssignmentEvent(event: CalendarEventBase) {
  return (
    event.type === 'scheduling.assignment' ||
    event.eventTypeCode === 'assignment' ||
    Boolean(resolveAssignmentEntryId(event))
  );
}

function resolveShiftEntryId(event: CalendarEventBase) {
  const metadata = (event as { metadata?: { shiftEntryId?: unknown } }).metadata;
  const parsed = Number(metadata?.shiftEntryId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}
