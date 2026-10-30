import type {
  SchedulingLeaveEntryRequest as LeaveEntryRequest,
  SchedulingLeaveSeriesRequest as LeaveSeriesRequest,
} from '@/api-access/generated/models';
import { defaultEndTime, defaultStartTime, toUtcIso } from '@/modules/scheduling/schedulingDateTime';
import type { SelectOption } from '@/types/select';
import type { LeaveFormData } from './components/LeaveForm.vue';
import { DateTime } from 'luxon';

export type LeaveRequestResult =
  | { kind: 'entry'; request: LeaveEntryRequest; error?: undefined }
  | { kind: 'series'; request: LeaveSeriesRequest; error?: undefined }
  | { error: string };

export function buildLeaveRequest(
  form: LeaveFormData,
  options: {
    locationId: number | null | undefined;
    timeZoneId: string;
    leaveSeriesId?: number | null;
    employeeOptions?: SelectOption[];
  },
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

  const allDay = form.allDay ?? false;
  let interval: { leaveDate: string } | { startAtUtc: string; endAtUtc: string };
  if (allDay) {
    if (!form.date || !/^\d{4}-\d{2}-\d{2}$/.test(form.date) || !DateTime.fromISO(form.date, { zone: 'UTC' }).isValid) {
      return { error: 'Select a valid date.' };
    }
    interval = { leaveDate: form.date };
  } else {
    const startAtUtc = toUtcIso(form.date, form.startTime || defaultStartTime, options.timeZoneId);
    const endAtUtc = toUtcIso(form.date, form.endTime || defaultEndTime, options.timeZoneId);
    if (!startAtUtc || !endAtUtc) return { error: 'Select a valid date and time.' };
    if (endAtUtc <= startAtUtc) return { error: 'End time must be after start time.' };
    interval = { startAtUtc, endAtUtc };
  }

  const commonRequest = {
    leaveTypeId,
    title: buildLeaveTitle(
      userIds.map(
        (userId) => options.employeeOptions?.find((option) => String(option.code) === userId)?.description || userId,
      ),
    ),
    notes: form.notes?.trim() || null,
    ...interval,
    timeZoneId: options.timeZoneId,
    allDay,
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

  return { kind: 'entry', request: { ...commonRequest, leaveSeriesId: options.leaveSeriesId ?? null } };
}

export function buildLeaveTitle(employeeNames: string[]) {
  const names = employeeNames.map((name) => name.trim()).filter(Boolean);
  return names.length > 0 ? `${names.join(', ')} leave` : 'Leave';
}
