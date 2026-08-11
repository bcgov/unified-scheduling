import { calendarActionRegistry } from '@/modules/calendar/registry/calendarActionRegistry';
import { calendarRegistry } from '@/modules/calendar/registry/calendarRegistry';
import {
  calendarAddAssignmentAction,
  calendarAddAssignmentResourceAction,
  calendarAddResourceAction,
  calendarDropAction,
  calendarDropUserOnAssignmentResourceAction,
  calendarEventBlockAction,
  calendarScheduleStaffAction,
  calendarSchedulingEventDetailAction,
  calendarSchedulingCreateShiftAction,
  calendarSchedulingHeaderDetailAction,
} from './calendarSchedulingActions';
import { calendarShiftViewContribution } from './calendarShiftViewContribution';
import { calendarAssignmentViewContribution } from './calendarAssignmentViewContribution';
import { calendarSchedulingAssignmentsContribution } from './contributions/calendarSchedulingAssignmentsContribution';
import {
  calendarSchedulingEventsContribution,
  clearSchedulingResourceDataCache,
} from './contributions/calendarSchedulingEventsContribution';

let isRegistered = false;

export function registerCalendarSchedulingModule() {
  if (isRegistered) {
    return;
  }

  calendarRegistry.registerModuleContribution(calendarSchedulingEventsContribution);
  calendarRegistry.registerModuleContribution(calendarSchedulingAssignmentsContribution);
  calendarRegistry.registerView(calendarShiftViewContribution);
  calendarRegistry.registerView(calendarAssignmentViewContribution);

  calendarActionRegistry.registerCreateAction(calendarSchedulingCreateShiftAction);
  calendarActionRegistry.registerDropAction(calendarDropAction);
  calendarActionRegistry.registerDropAction(calendarDropUserOnAssignmentResourceAction);
  calendarActionRegistry.registerMatrixSidePanelAction(calendarAddAssignmentAction);
  calendarActionRegistry.registerMatrixSidePanelAction(calendarScheduleStaffAction);
  calendarActionRegistry.registerMatrixResourceAction(calendarAddResourceAction);
  calendarActionRegistry.registerMatrixResourceAction(calendarAddAssignmentResourceAction);
  calendarActionRegistry.registerMatrixCellHeaderAction(calendarSchedulingHeaderDetailAction);
  calendarActionRegistry.registerMatrixEventBlockAction(calendarEventBlockAction);
  calendarActionRegistry.registerViewDetailAction(
    calendarShiftViewContribution.id,
    calendarSchedulingEventDetailAction,
  );
  calendarActionRegistry.registerViewDetailAction(
    calendarAssignmentViewContribution.id,
    calendarSchedulingEventDetailAction,
  );

  isRegistered = true;
}

export const registerModule = registerCalendarSchedulingModule;
export const clearResourceDataCache = clearSchedulingResourceDataCache;
