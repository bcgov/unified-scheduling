import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type {
  CalendarDropAction,
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
  showCalendarSchedulingDropModal,
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
  label: 'Add assignment',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.addAssignment,
  execute: () => {
    showCalendarSchedulingAssignmentModal();
  },
};

export const calendarAddResourceAction: CalendarMatrixResourceAction = {
  id: calendarSchedulingActionIds.addResource,
  moduleId: 'calendar-scheduling',
  label: 'Add resource',
  order: 10,
  isAvailable: (context) => context.actionId === calendarSchedulingActionIds.addResource,
  execute: (context) => {
    showCalendarSchedulingResourceActionModal(context.resource, context.cell?.date);
  },
};

export const calendarDropAction: CalendarDropAction = {
  id: 'calendar-scheduling.drop-assignment-on-resource',
  moduleId: 'calendar-scheduling',
  label: 'Drop assignment on resource',
  order: 10,
  isAvailable: (drag, drop) => ['assignment', 'user'].includes(drag.itemType) && Boolean(drop.resourceId),
  execute: async (drag, drop) => {
    showCalendarSchedulingDropModal(drag, drop);
  },
};

export const calendarEventBlockAction: CalendarMatrixEventBlockAction = {
  id: calendarSchedulingActionIds.addOnEvent,
  moduleId: 'calendar-scheduling',
  label: 'Add on event',
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
  label: 'Show conflict',
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
  label: 'Resolve conflict',
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
  isAvailable: (context) => context.event.sourceModule === 'calendar-scheduling',
  run: (context) => {
    showCalendarSchedulingEventDetail(context.event);
  },
};

export const calendarSchedulingHeaderDetailAction: CalendarMatrixCellHeaderAction = {
  id: calendarSchedulingActionIds.viewHeaderDetails,
  moduleId: 'calendar-scheduling',
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
  moduleId: 'calendar-scheduling',
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
  moduleId: 'calendar-scheduling',
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
