import { calendarActionRegistry } from '@/modules/calendar/registry/calendarActionRegistry';
import { calendarRegistry } from '@/modules/calendar/registry/calendarRegistry';
import {
  calendarMatrixTestAddAssignmentAction,
  calendarMatrixTestAddResourceAction,
  calendarMatrixTestDropAction,
  calendarMatrixTestEventBlockAction,
  calendarMatrixTestEventDetailAction,
  calendarMatrixTestHeaderDetailAction,
  calendarMatrixTestHeaderResolveConflictAction,
  calendarMatrixTestHeaderShowConflictAction,
  calendarMatrixTestResolveConflictAction,
  calendarMatrixTestShowConflictAction,
} from './calendarMatrixTestActions';
import { calendarMatrixScheduleTestViewContribution } from './calendarMatrixTestScheduleViewContribution';
import { calendarMatrixAssignmentTestViewContribution } from './calendarMatrixTestAssignmentViewContribution';

let isRegistered = false;

export function registerCalendarMatrixTestModule() {
  if (isRegistered) {
    return;
  }

  calendarRegistry.registerView(calendarMatrixScheduleTestViewContribution);
  calendarRegistry.registerView(calendarMatrixAssignmentTestViewContribution);
  calendarActionRegistry.registerViewDetailAction(
    calendarMatrixScheduleTestViewContribution.id,
    calendarMatrixTestEventDetailAction,
  );
  calendarActionRegistry.registerViewDetailAction(
    calendarMatrixAssignmentTestViewContribution.id,
    calendarMatrixTestEventDetailAction,
  );

  calendarActionRegistry.registerDropAction(calendarMatrixTestDropAction);
  calendarActionRegistry.registerMatrixSidePanelAction(calendarMatrixTestAddAssignmentAction);
  calendarActionRegistry.registerMatrixResourceAction(calendarMatrixTestAddResourceAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarMatrixTestHeaderDetailAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarMatrixTestHeaderShowConflictAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarMatrixTestHeaderResolveConflictAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarMatrixTestEventBlockAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarMatrixTestShowConflictAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarMatrixTestResolveConflictAction);

  isRegistered = true;
}

export const registerModule = registerCalendarMatrixTestModule;
