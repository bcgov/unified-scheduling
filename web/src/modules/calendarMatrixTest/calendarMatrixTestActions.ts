import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type {
  CalendarDropAction,
  CalendarMatrixCellHeaderAction,
  CalendarMatrixEventBlockAction,
  CalendarMatrixResourceAction,
  CalendarMatrixSidePanelAction,
  CalendarViewDetailAction,
} from '@/modules/calendar/registry/calendarActionRegistryTypes';
import { calendarMatrixTestActionIds } from './calendarMatrixTestActionIds';
import {
  closeCalendarMatrixTestConflict,
  showCalendarMatrixTestAssignmentModal,
  showCalendarMatrixTestDropModal,
  showCalendarMatrixTestEventActionModal,
  showCalendarMatrixTestEventDetail,
  showCalendarMatrixTestResourceActionModal,
  toggleCalendarMatrixTestConflict,
} from './calendarMatrixTestState';

export const calendarMatrixTestAddAssignmentAction: CalendarMatrixSidePanelAction = {
  id: calendarMatrixTestActionIds.addAssignment,
  moduleId: 'calendar-matrix-test',
  label: 'Add assignment',
  order: 10,
  isAvailable: (context) => context.actionId === calendarMatrixTestActionIds.addAssignment,
  execute: () => {
    showCalendarMatrixTestAssignmentModal();
  },
};

export const calendarMatrixTestAddResourceAction: CalendarMatrixResourceAction = {
  id: calendarMatrixTestActionIds.addResource,
  moduleId: 'calendar-matrix-test',
  label: 'Add resource',
  order: 10,
  isAvailable: (context) => context.actionId === calendarMatrixTestActionIds.addResource,
  execute: (context) => {
    showCalendarMatrixTestResourceActionModal(context.resource);
  },
};

export const calendarMatrixTestDropAction: CalendarDropAction = {
  id: 'calendar-matrix-test.drop-assignment-on-resource',
  moduleId: 'calendar-matrix-test',
  label: 'Drop assignment on resource',
  order: 10,
  isAvailable: (drag, drop) => ['assignment', 'user'].includes(drag.itemType) && Boolean(drop.resourceId),
  execute: async (drag, drop) => {
    showCalendarMatrixTestDropModal(drag, drop);
  },
};

export const calendarMatrixTestEventBlockAction: CalendarMatrixEventBlockAction = {
  id: calendarMatrixTestActionIds.addOnEvent,
  moduleId: 'calendar-matrix-test',
  label: 'Add on event',
  order: 10,
  isAvailable: (context) =>
    context.actionId === calendarMatrixTestActionIds.addOnEvent &&
    context.event.sourceModule === 'calendar-matrix-test',
  execute: (context) => {
    showCalendarMatrixTestEventActionModal(context.event);
  },
};

export const calendarMatrixTestShowConflictAction: CalendarMatrixEventBlockAction = {
  id: calendarMatrixTestActionIds.showConflict,
  moduleId: 'calendar-matrix-test',
  label: 'Show conflict',
  order: 20,
  isAvailable: (context) =>
    context.actionId === calendarMatrixTestActionIds.showConflict &&
    context.event.sourceModule === 'calendar-matrix-test',
  execute: (context) => {
    toggleCalendarMatrixTestConflict(context.event.id);
  },
};

export const calendarMatrixTestResolveConflictAction: CalendarMatrixEventBlockAction = {
  id: calendarMatrixTestActionIds.resolveConflict,
  moduleId: 'calendar-matrix-test',
  label: 'Resolve conflict',
  order: 30,
  isAvailable: (context) =>
    context.actionId === calendarMatrixTestActionIds.resolveConflict &&
    context.event.sourceModule === 'calendar-matrix-test',
  execute: (_context) => {
    closeCalendarMatrixTestConflict();
  },
};

export const calendarMatrixTestEventDetailAction: CalendarViewDetailAction = {
  id: 'calendar-matrix-test.event-detail.modal',
  moduleId: 'calendar-matrix-test',
  isAvailable: (context) => context.event.sourceModule === 'calendar-matrix-test',
  run: (context) => {
    showCalendarMatrixTestEventDetail(context.event);
  },
};

export const calendarMatrixTestHeaderDetailAction: CalendarMatrixCellHeaderAction = {
  id: calendarMatrixTestActionIds.viewHeaderDetails,
  moduleId: 'calendar-matrix-test',
  label: 'View header details',
  order: 10,
  isAvailable: (context) =>
    context.actionId === calendarMatrixTestActionIds.viewHeaderDetails && isCalendarEventBase(context.header.payload),
  execute: (context) => {
    if (isCalendarEventBase(context.header.payload)) {
      showCalendarMatrixTestEventDetail(context.header.payload);
    }
  },
};

export const calendarMatrixTestHeaderShowConflictAction: CalendarMatrixCellHeaderAction = {
  id: calendarMatrixTestActionIds.showConflict,
  moduleId: 'calendar-matrix-test',
  label: 'Show header conflict',
  order: 20,
  isAvailable: (context) =>
    context.actionId === calendarMatrixTestActionIds.showConflict && isCalendarEventBase(context.header.payload),
  execute: (context) => {
    if (isCalendarEventBase(context.header.payload)) {
      toggleCalendarMatrixTestConflict(context.header.payload.id);
    }
  },
};

export const calendarMatrixTestHeaderResolveConflictAction: CalendarMatrixCellHeaderAction = {
  id: calendarMatrixTestActionIds.resolveConflict,
  moduleId: 'calendar-matrix-test',
  label: 'Resolve header conflict',
  order: 30,
  isAvailable: (context) =>
    context.actionId === calendarMatrixTestActionIds.resolveConflict && isCalendarEventBase(context.header.payload),
  execute: (context) => {
    if (isCalendarEventBase(context.header.payload)) {
      closeCalendarMatrixTestConflict();
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
