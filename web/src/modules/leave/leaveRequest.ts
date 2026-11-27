import type {
  SchedulingLeaveEntryRequest as LeaveEntryRequest,
  SchedulingLeaveSeriesRequest as LeaveSeriesRequest,
  SchedulingLeaveSeriesResponse as LeaveSeriesResponse,
} from '@/api-access/generated/models';
import { defaultEndTime, defaultStartTime, parseFormDateTime, toUtcIso } from '@/modules/scheduling/schedulingDateTime';
import type { SelectOption } from '@/types/select';
import type { LeaveFormData } from './components/LeaveForm.vue';
import { DateTime } from 'luxon';
import { Temporal } from 'temporal-polyfill';

type LeaveSchedule = Pick<LeaveSeriesResponse, 'allDay' | 'leaveDate' | 'startAtUtc' | 'endAtUtc'>;

export function getLeaveScheduleFields(schedule: LeaveSchedule, timeZoneId: string) {
  const { allDay = false, leaveDate, startAtUtc, endAtUtc } = schedule;
  const start = startAtUtc ? parseFormDateTime(startAtUtc, timeZoneId) : null;
  const end = endAtUtc ? parseFormDateTime(endAtUtc, timeZoneId) : null;
  return {
    allDay,
    date: (allDay ? leaveDate : undefined) ?? start?.date ?? '',
    startTime: allDay ? defaultStartTime : (start?.time ?? defaultStartTime),
    endDate: allDay ? undefined : end?.date,
    endTime: allDay ? defaultEndTime : (end?.time ?? null),
  };
}

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
    persistedLeave?: LeaveSchedule & Pick<LeaveSeriesResponse, 'description'>;
    fixedSeriesSchedule?: LeaveSeriesResponse;
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

  const persistedLeave = options.fixedSeriesSchedule ?? options.persistedLeave;
  const commonRequest = {
    leaveTypeId,
    title: buildLeaveTitle(
      userIds.map(
        (userId) => options.employeeOptions?.find((option) => String(option.code) === userId)?.description || userId,
      ),
    ),
    ...(persistedLeave && { description: persistedLeave.description ?? null }),
    notes: form.notes?.trim() || null,
    locationId: options.locationId,
    userIds,
  };

  if (options.fixedSeriesSchedule) {
    // Copy the persisted interval without passing it through the form's minute-precision inputs.
    const { allDay, leaveDate, startAtUtc, endAtUtc, timeZoneId, recurrenceRule } = options.fixedSeriesSchedule;
    if (!recurrenceRule) return { error: 'Set a valid recurrence rule.' };
    if (allDay && !leaveDate) return { error: 'Select a valid date.' };
    if (!allDay && (!startAtUtc || !endAtUtc)) return { error: 'Select a valid date and time.' };
    return {
      kind: 'series',
      request: {
        ...commonRequest,
        ...(allDay ? { leaveDate } : { startAtUtc, endAtUtc }),
        allDay: allDay ?? false,
        timeZoneId: timeZoneId ?? null,
        recurrenceRule,
      },
    };
  }

  const allDay = form.allDay ?? false;
  let interval: { leaveDate: string } | { startAtUtc: string; endAtUtc: string | null };
  if (allDay) {
    if (!form.date || !/^\d{4}-\d{2}-\d{2}$/.test(form.date) || !DateTime.fromISO(form.date, { zone: 'UTC' }).isValid) {
      return { error: 'Select a valid date.' };
    }
    interval = { leaveDate: form.date };
  } else {
    const persisted =
      persistedLeave && !persistedLeave.allDay ? getLeaveScheduleFields(persistedLeave, options.timeZoneId) : undefined;
    const startTime = form.startTime || defaultStartTime;
    const endDate = form.endDate ?? form.date;
    // Preserve explicit absence on entries; uninitialized creation values still use the default.
    const endTime = form.endTime === null ? null : form.endTime || defaultEndTime;
    // The form only captures minutes, and repeated local times can refer to different instants.
    const startAtUtc =
      persisted?.date && form.date === persisted.date && startTime === persisted.startTime
        ? persistedLeave?.startAtUtc
        : toUtcIso(form.date, startTime, options.timeZoneId);
    const endAtUtc =
      endTime === null
        ? null
        : endDate === persisted?.endDate && endTime === persisted?.endTime
          ? (persistedLeave?.endAtUtc ?? null)
          : toUtcIso(endDate, endTime, options.timeZoneId);
    if (!startAtUtc || (endTime !== null && !endAtUtc) || (form.repeatMode === 'custom' && !endAtUtc)) {
      return { error: 'Select a valid date and time.' };
    }
    if (endAtUtc && Temporal.Instant.compare(endAtUtc, startAtUtc) <= 0) {
      return { error: 'End time must be after start time.' };
    }
    interval = { startAtUtc, endAtUtc };
  }

  const request = { ...commonRequest, ...interval, timeZoneId: options.timeZoneId, allDay };
  if (form.repeatMode === 'custom') {
    if (!form.recurrenceRule) {
      return { error: 'Set a valid recurrence rule.' };
    }
    return {
      kind: 'series',
      request: { ...request, recurrenceRule: form.recurrenceRule },
    };
  }

  return { kind: 'entry', request: { ...request, leaveSeriesId: options.leaveSeriesId ?? null } };
}

export function buildLeaveTitle(employeeNames: string[]) {
  const names = employeeNames.map((name) => name.trim()).filter(Boolean);
  return names.length > 0 ? `${names.join(', ')} leave` : 'Leave';
}
