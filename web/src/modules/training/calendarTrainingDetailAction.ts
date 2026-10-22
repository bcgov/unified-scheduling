import { Permissions } from '@/api-access/generated/models';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import type { CalendarMatrixCellHeaderAction } from '@/modules/calendar/registry/calendarActionRegistryTypes';
import { trainingCalendarDetailActionId } from './calendarTrainingIdentifiers';
import { isTrainingCalendarEvent } from './contributions/calendarTrainingEventsContribution';

export const calendarTrainingDetailAction: CalendarMatrixCellHeaderAction = {
  id: trainingCalendarDetailActionId,
  moduleId: 'training',
  isAvailable: (context, runtimeContext) =>
    context.actionId === trainingCalendarDetailActionId &&
    runtimeContext.featureFlags.Training?.enabled === true &&
    runtimeContext.permissions?.includes(Permissions.UserTrainingsView) === true &&
    isTrainingCalendarEvent(context.header.payload),
  execute: (context) => {
    if (isTrainingCalendarEvent(context.header.payload)) {
      useCalendarStore().setSelectedEvent(context.header.payload.id);
    }
  },
};
