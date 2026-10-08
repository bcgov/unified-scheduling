import type {
  SchedulingTimeOffEntryRequest as TimeOffEntryRequest,
  SchedulingTimeOffSeriesRequest as TimeOffSeriesRequest,
} from '@/api-access/generated/models';
import { defaultEndTime, defaultStartTime, toUtcIso } from '@/modules/scheduling/schedulingDateTime';
import type { TimeOffFormData } from './components/TimeOffForm.vue';

export type TimeOffRequestResult =
  | { kind: 'entry'; request: TimeOffEntryRequest; error?: undefined }
  | { kind: 'series'; request: TimeOffSeriesRequest; error?: undefined }
  | { error: string };

export function buildTimeOffRequest(
  form: TimeOffFormData,
  options: { locationId: number | null | undefined; timeZoneId: string },
): TimeOffRequestResult {
  const userIds = (form.userIds ?? []).filter((id): id is string => typeof id === 'string' && id.length > 0);
  const leaveTypeId = Number(form.leaveType);

  if (userIds.length === 0) {
    return { error: 'Select at least one employee.' };
  }

  if (!Number.isInteger(leaveTypeId) || leaveTypeId <= 0) {
    return { error: 'Select a leave type.' };
  }

  if (options.locationId == null) {
    return { error: 'Select a location before adding time off.' };
  }

  const startAtUtc = toUtcIso(form.date, form.startTime || defaultStartTime, options.timeZoneId);
  const endAtUtc = toUtcIso(form.date, form.endTime || defaultEndTime, options.timeZoneId);
  if (!startAtUtc || !endAtUtc) {
    return { error: 'Select a valid date and time.' };
  }

  if (endAtUtc <= startAtUtc) {
    return { error: 'End time must be after start time.' };
  }

  const commonRequest = {
    leaveTypeId,
    title: 'Time Off',
    notes: form.notes?.trim() || null,
    startAtUtc,
    endAtUtc,
    timeZoneId: options.timeZoneId,
    allDay: false,
    locationId: options.locationId,
    userIds,
  };

  if (form.repeatMode === 'custom') {
    if (!form.recurrenceRule) {
      return { error: 'Set a valid recurrence rule.' };
    }
    return {
      kind: 'series',
      request: { ...commonRequest, recurrenceRule: form.recurrenceRule },
    };
  }

  return { kind: 'entry', request: commonRequest };
}
