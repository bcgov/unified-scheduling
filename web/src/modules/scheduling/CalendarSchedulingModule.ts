import { calendarActionRegistry } from '@/modules/calendar/registry/calendarActionRegistry';
import { calendarRegistry } from '@/modules/calendar/registry/calendarRegistry';
import {
  calendarAddAssignmentAction,
  calendarAddResourceAction,
  calendarDropAction,
  calendarEventBlockAction,
  calendarSchedulingCreateShiftAction,
  calendarSchedulingHeaderDetailAction,
  calendarSchedulingHeaderResolveConflictAction,
  calendarSchedulingHeaderShowConflictAction,
  calendarSchedulingResolveConflictAction,
  calendarSchedulingShowConflictAction,
} from './calendarSchedulingActions';
import { calendarShiftViewContribution } from './calendarShiftViewContribution';
import { calendarAssignmentViewContribution } from './calendarAssignmentViewContribution';
import { calendarSchedulingEventsContribution } from './contributions/calendarSchedulingEventsContribution';

let isRegistered = false;

export function registerCalendarSchedulingModule() {
  if (isRegistered) {
    return;
  }

  calendarRegistry.registerModuleContribution(calendarSchedulingEventsContribution);
  calendarRegistry.registerView(calendarShiftViewContribution);
  calendarRegistry.registerView(calendarAssignmentViewContribution);

  calendarActionRegistry.registerCreateAction(calendarSchedulingCreateShiftAction);
  calendarActionRegistry.registerDropAction(calendarDropAction);
  calendarActionRegistry.registerMatrixSidePanelAction(calendarAddAssignmentAction);
  calendarActionRegistry.registerMatrixResourceAction(calendarAddResourceAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarSchedulingHeaderDetailAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarSchedulingHeaderShowConflictAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarSchedulingHeaderResolveConflictAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarEventBlockAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarSchedulingShowConflictAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarSchedulingResolveConflictAction);

  isRegistered = true;
}

export const registerModule = registerCalendarSchedulingModule;
