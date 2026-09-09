import type { WorkingHoursResult } from '@/api-access/generated/models';
import type { CalendarDataResponse } from '@/modules/calendar/calendarTypes';
import { selectContribution } from '@/modules/calendar/calendarSelectors';
import {
  schedulingWorkingHoursContributionId,
  type CalendarSchedulingWorkingHoursContributionData,
} from './contributions/calendarSchedulingWorkingHoursContribution';

export function createWorkingHoursLookup(response: CalendarDataResponse) {
  const data = selectContribution(response, schedulingWorkingHoursContributionId)?.data;
  const results = isWorkingHoursContributionData(data) ? data.results : [];
  const byShiftAndUser = new Map(
    results.map((result) => [createWorkingHoursKey(result.shiftEntryId, result.userId), result] as const),
  );

  return {
    get(shiftEntryId: number, userId: string) {
      return byShiftAndUser.get(createWorkingHoursKey(shiftEntryId, userId));
    },
  };
}

export function formatWorkingHoursMinutes(totalMinutes: number) {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours === 0) {
    return `${minutes}m`;
  }

  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`;
}

function createWorkingHoursKey(shiftEntryId: number, userId: string) {
  return `${shiftEntryId}:${userId}`;
}

function isWorkingHoursContributionData(value: unknown): value is CalendarSchedulingWorkingHoursContributionData {
  return typeof value === 'object' && value !== null && 'results' in value && Array.isArray(value.results);
}
