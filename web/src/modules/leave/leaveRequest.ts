import type {
  SchedulingLeaveEntryRequest as LeaveEntryRequest,
  SchedulingLeaveSeriesRequest as LeaveSeriesRequest,
} from '@/api-access/generated/models';
import { defaultEndTime, defaultStartTime, toUtcIso } from '@/modules/scheduling/schedulingDateTime';
import type { LeaveFormData } from './components/LeaveForm.vue';

export type LeaveRequestResult =
  | { kind: 'entry'; request: LeaveEntryRequest; error?: undefined }
  | { kind: 'series'; request: LeaveSeriesRequest; error?: undefined }
  | { error: string };

export function buildLeaveRequest(
  form: LeaveFormData,
  options: { locationId: number | null | undefined; timeZoneId: string },
): LeaveRequestResult {
  const userIds = (form.userIds ?? []).filter((id): id is string => typeof id === 'string' && id.length > 0);
  const leaveTypeId = Number(form.leaveType);

  if (userIds.length === 0) {
    return { error: 'Select at least one employee.' };
  }

  if (!Number.isInteger(leaveTypeId) || leaveTypeId <= 0) {
    return { error: 'Select a leave type.' };
  }

  if (options.locationId == null) {
    return { error: 'Select a location before adding leave.' };
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
    title: 'Leave',
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
