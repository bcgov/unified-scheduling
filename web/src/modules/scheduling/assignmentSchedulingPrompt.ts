import { CalendarContributionId } from '@/modules/calendar/calendarIdentifiers';
import { selectContribution } from '@/modules/calendar/calendarSelectors';
import type { CalendarDataResponse, CalendarQueryContext } from '@/modules/calendar/calendarTypes';
import { schedulingAssignmentResourcesContributionId } from './contributions/calendarSchedulingAssignmentResourcesContribution';
import { eventStartsInCalendarContext } from './calendarSchedulingDate';
import { isSchedulingCancelled } from './schedulingLifecycle';

const assignmentSchedulingPrompt =
  'Schedule an assignment by dragging an assignment type onto a day cell for the row of a desired team member.';
const assignmentSchedulingClickPrompt =
  'Schedule an assignment by clicking the + button beside an assignment type or in its day cell.';

export function getAssignmentSchedulingPrompt(
  response: CalendarDataResponse,
  context: CalendarQueryContext,
  interaction: 'drag' | 'click' = 'drag',
) {
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

  const hasShift = scheduling.events.some(
    (event) =>
      event.locationId === locationId &&
      (event.type === 'scheduling.shift' || event.eventTypeCode === 'shift') &&
      !isSchedulingCancelled(event.statusTypeCode) &&
      eventStartsInCalendarContext(event, context),
  );
  const hasAssignment = scheduling.events.some(
    (event) =>
      event.locationId === locationId &&
      (event.type === 'scheduling.assignment' || event.eventTypeCode === 'assignment') &&
      eventStartsInCalendarContext(event, context),
  );

  return hasShift && !hasAssignment
    ? interaction === 'click'
      ? assignmentSchedulingClickPrompt
      : assignmentSchedulingPrompt
    : undefined;
}
