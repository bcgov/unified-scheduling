import { calendarActionRegistry } from '@/modules/calendar/registry/calendarActionRegistry';
import { calendarRegistry } from '@/modules/calendar/registry/calendarRegistry';
import {
  calendarAddResourceAction,
  calendarSchedulingCreateShiftAction,
  calendarSchedulingHeaderDetailAction,
  calendarSchedulingHeaderResolveConflictAction,
  calendarSchedulingHeaderShowConflictAction,
  calendarSchedulingResolveConflictAction,
  calendarSchedulingShowConflictAction,
} from './calendarSchedulingActions';
import { calendarShiftViewContribution } from './calendarShiftViewContribution';
import { calendarSchedulingEventsContribution } from './contributions/calendarSchedulingEventsContribution';

let isRegistered = false;

export function registerCalendarSchedulingModule() {
  if (isRegistered) {
    return;
  }

  calendarRegistry.registerModuleContribution(calendarSchedulingEventsContribution);
  calendarRegistry.registerView(calendarShiftViewContribution);

  calendarActionRegistry.registerCreateAction(calendarSchedulingCreateShiftAction);
  calendarActionRegistry.registerMatrixResourceAction(calendarAddResourceAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarSchedulingHeaderDetailAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarSchedulingHeaderShowConflictAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarSchedulingHeaderResolveConflictAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarSchedulingShowConflictAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarSchedulingResolveConflictAction);

  isRegistered = true;
}

export const registerModule = registerCalendarSchedulingModule;
