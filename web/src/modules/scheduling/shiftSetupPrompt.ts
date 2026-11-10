import { CalendarContributionId } from '@/modules/calendar/calendarIdentifiers';
import { selectContribution } from '@/modules/calendar/calendarSelectors';
import type { CalendarDataResponse, CalendarQueryContext } from '@/modules/calendar/calendarTypes';
import { schedulingAssignmentResourcesContributionId } from './contributions/calendarSchedulingAssignmentResourcesContribution';
import { eventStartsInCalendarContext } from './calendarSchedulingDate';
import { isSchedulingCancelled } from './schedulingLifecycle';

const shiftSetupPrompt = 'Assign shifts to staff before scheduling assignments.';

export function getShiftSetupPrompt(response: CalendarDataResponse, context: CalendarQueryContext) {
  const { locationId } = context;
  if (!locationId) return undefined;

  const assignmentTypes = selectContribution(response, schedulingAssignmentResourcesContributionId);
  const scheduling = selectContribution(response, CalendarContributionId.SchedulingEvents);
  const hasAssignmentType = assignmentTypes?.resources?.some(
    (resource) => 'locationId' in resource && resource.locationId === locationId,
  );
  if (!hasAssignmentType || !scheduling) {
    return undefined;
  }

  const hasActiveShift = scheduling.events.some(
    (event) =>
      event.locationId === locationId &&
      (event.type === 'scheduling.shift' || event.eventTypeCode === 'shift') &&
      !isSchedulingCancelled(event.statusTypeCode) &&
      eventStartsInCalendarContext(event, context),
  );
  return hasActiveShift ? undefined : shiftSetupPrompt;
}
