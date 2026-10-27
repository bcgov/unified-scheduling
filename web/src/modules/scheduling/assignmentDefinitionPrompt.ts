import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import { selectContribution } from '@/modules/calendar/calendarSelectors';
import type { CalendarDataResponse } from '@/modules/calendar/calendarTypes';
import { addDays, startOfWeek } from '@/utils/date';
import { assignmentDefinitionOverlapsCalendarDateRange } from './assignmentDefinitionDateHelpers';
import {
  schedulingAssignmentResourcesContributionId,
  type CalendarSchedulingAssignmentResourcesData,
} from './contributions/calendarSchedulingAssignmentResourcesContribution';

export const missingAssignmentTypePrompt = 'Create an assignment type to start scheduling assignments.';

export function hasNoAssignmentTypesForWeek(
  definitions: AssignmentDefinitionResponse[],
  locationId: number | null | undefined,
  date: string,
  timeZoneId: string,
) {
  if (!locationId) return false;

  const startDate = startOfWeek(date);
  const endDate = addDays(startDate, 7);
  return !definitions.some(
    (definition) =>
      definition.locationId === locationId &&
      typeof definition.id === 'number' &&
      definition.id > 0 &&
      assignmentDefinitionOverlapsCalendarDateRange(definition, startDate, endDate, timeZoneId),
  );
}

export function getMissingAssignmentTypePrompt(
  response: CalendarDataResponse,
  locationId: number | null | undefined,
  date: string,
  timeZoneId: string,
) {
  const contribution = selectContribution(response, schedulingAssignmentResourcesContributionId);
  const definitions = (contribution?.data as CalendarSchedulingAssignmentResourcesData | undefined)?.definitions;
  return definitions && hasNoAssignmentTypesForWeek(definitions, locationId, date, timeZoneId)
    ? missingAssignmentTypePrompt
    : undefined;
}
