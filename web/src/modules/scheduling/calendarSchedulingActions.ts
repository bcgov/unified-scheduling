import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { CalendarModuleId } from '@/modules/calendar/calendarIdentifiers';
import type {
  CalendarCreateAction,
  CalendarMatrixCellHeaderAction,
  CalendarMatrixEventBlockAction,
  CalendarMatrixResourceAction,
} from '@/modules/calendar/registry/calendarActionRegistryTypes';
import { calendarSchedulingActionIds } from './calendarSchedulingActionIds';
import {
  closeCalendarSchedulingConflict,
  showCalendarSchedulingEventDetail,
  showCalendarSchedulingResourceActionModal,
  toggleCalendarSchedulingConflict,
} from './calendarSchedulingState';
import { calendarShiftViewContribution } from './calendarShiftViewContribution';

export const calendarSchedulingCreateShiftAction: CalendarCreateAction = {
  id: calendarSchedulingActionIds.createShift,
  moduleId: CalendarModuleId.SchedulingUi,
  label: '+ Create Shift',
  isAvailable: (context) => context.activeViewId === calendarShiftViewContribution.id,
  run: (context) => {
    showCalendarSchedulingResourceActionModal(undefined, context.startDate);
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
